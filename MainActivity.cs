#pragma warning disable CA1416

using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.Provider;
using Android.Runtime;
using Android.Text.Method;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using System.Net;
using System.Text;
using System.Xml;
using System.Linq;
using Xamarin.Essentials;
using SysTrace = System.Diagnostics.Trace;  // use "app_process64" in logact
using SysEnv = System.Environment;
using ASCOM.Tools;
using ASCOM.Common.Interfaces;
using ASCOM.Tools.Novas31;
using ASCOM.Tools.NovasCom;
using System.Globalization;

namespace AscomLibTest {
    [Activity(Label = "@string/app_name", MainLauncher = true, Name = "a.b.C1", Theme = "@style/Theme.AppCompat", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
    public class MainActivity : AppCompatActivity, ILocationListener {
        private LocationManager lm;
        private double longitude = 0.0;
        private double latitude = 0.0;
        private bool LocationIsKnown = false;
        private readonly (double Height, double Temperature, double Pressure) ObserverSite = (
            111,
            20.0,
            1011.0
        );

        protected override async void OnCreate(Bundle? savedInstanceState) {
            base.OnCreate(savedInstanceState);
            Platform.Init(this, savedInstanceState);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            // request for permissions at runtime
            if (await Permissions.CheckStatusAsync<Permissions.StorageRead>() != PermissionStatus.Granted) await Permissions.RequestAsync<Permissions.StorageRead>();
            if (await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() != PermissionStatus.Granted) await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            // setup GPS location management
            lm = LocationManager.FromContext(this);
            //foreach (var lprov in lm.AllProviders) SysTrace.WriteLine(lprov, "Location provider");
            if (!lm.IsProviderEnabled(LocationManager.GpsProvider) && !lm.IsProviderEnabled(LocationManager.NetworkProvider)) {
                Toast.MakeText(this, "Please turn on GPS!", ToastLength.Short)?.Show();
                StartActivity(new Intent(Settings.ActionLocationSourceSettings));
            }

            // copy "JPLEPH" and "cio_ra.bin" raw resources to the user directory
            var pp = SysEnv.GetFolderPath(SysEnv.SpecialFolder.UserProfile);
            var eph_path = Path.Combine(pp, "JPLEPH");
            if (!File.Exists(eph_path)) {
                using (var stw = new StreamWriter(eph_path))
                    using (var str = Resources.OpenRawResource(Resource.Raw.JPLEPH))
                        await str.CopyToAsync(stw.BaseStream);
            }
            var cio_path = Path.Combine(pp, "cio_ra.bin");
            if (!File.Exists(cio_path)) {
                using (var stw = new StreamWriter(cio_path))
                    using (var str = Resources.OpenRawResource(Resource.Raw.cio_ra))
                        await str.CopyToAsync(stw.BaseStream);
            }
            
            // setup the UI: one textview as a console output and one button at the bottom
            var txv1 = FindViewById<AppCompatTextView>(Resource.Id.txv1);
            txv1.SetHorizontallyScrolling(true);
            txv1.MovementMethod = new ScrollingMovementMethod();

            var edt2 = FindViewById<AppCompatEditText>(Resource.Id.edt2);

            // setup AscomLibrary custom logger and assign it to the library classes
            var al = new AndroidLogger(txv1);
            al.SetMinimumLoggingLevel(LogLevel.Information);
            Novas.SetLogger(al);
            AstroUtilities.SetLogger(al);
            
            // handle button tap
            var btn1 = FindViewById<AppCompatButton>(Resource.Id.btn1);
            btn1.Click += async (s, e) => {
                var J2000 = Novas.JulianDate(2000, 1, 1, 12);   // NOVAS call
                txv1.Text = $"NOVAS J2000.0: {J2000}{SysEnv.NewLine}";

                var jdUTC = AstroUtilities.JulianDateUtc;   // SOFA call
                txv1.Append($"SOFA JD now: {jdUTC}{SysEnv.NewLine}{SysEnv.NewLine}");

                var jdTT = jdUTC + AstroUtilities.DeltaT(jdUTC) / 86400.0;

                var Venus = new SolarSystemBody(Body.Venus) {
                    SiteLatitude    = latitude,
                    SiteLongitude   = longitude,
                    SiteHeight      = ObserverSite.Height //,
                    //SiteTemperature = ObserverSite.Temperature,
                    //SitePressure    = ObserverSite.Pressure
                };

                // do NOT specify UTC time, it will be converted anyway
                var now = DateTime.Now;
                
                // calculate Venus current horizontaly coordinates
                var coord = Venus.AltAzCoordinates(now);
                var azm = Utilities.DegreesToDMS(coord.Azimuth, "°", "′", "″", 0);
                var alt = Utilities.DegreesToDMS(coord.Altitude, "°", "′", "″", 0);
                txv1.Append($"{SysEnv.NewLine}Venus horizontal coordinates{SysEnv.NewLine}");
                txv1.Append($"Azimuth:  {azm}{SysEnv.NewLine}");
                txv1.Append($"Altitude: {alt}{SysEnv.NewLine}{SysEnv.NewLine}");

                // calculate current Moon rise and set times
                if (LocationIsKnown) {
                    var tzo = TimeZoneInfo.Local.GetUtcOffset(now).TotalHours;
                    SysTrace.WriteLine(tzo, "Time zone offset");
                    var RiseaAndSet = AstroUtilities.EventTimes(EventType.MoonRiseMoonSet, now.Day, now.Month, now.Year, latitude, longitude, tzo);
                    foreach (var evt in RiseaAndSet.RiseEvents) txv1.Append($"{SysEnv.NewLine}Moon rises at: {TimeSpan.FromHours(evt):hh\\:mm\\:ss}{SysEnv.NewLine}");
                    foreach (var evt in RiseaAndSet.SetEvents) txv1.Append($"Moon sets  at: {TimeSpan.FromHours(evt):hh\\:mm\\:ss}{SysEnv.NewLine}");
                }
                else txv1.Append($"Location is not yet known,{SysEnv.NewLine}cannot calculate Moonrise and Moonset times.{SysEnv.NewLine}{SysEnv.NewLine}");

                var site = new Site() {
                    Height = 100,
                    Pressure = 1000.0,
                    Temperature = 20.0,
                    Latitude = latitude,
                    Longitude = longitude
                };

                // download star data from SIMBAD using URL query,
                // more info here: https://simbad.cds.unistra.fr/guide/sim-url.htx
                txv1.Append($"{SysEnv.NewLine}Downloading {edt2.Text} data from SIMBAD...{SysEnv.NewLine}");
                var wc = new WebClient() { Encoding = Encoding.UTF8 };
                var doc = new XmlDocument();
                var XPathBase = "/*[local-name() = 'VOTABLE']/*[local-name() = 'RESOURCE']";
                try {
                    // download to a VOTABLE XML format,
                    // more info here: https://www.ivoa.net/documents/VOTable/20250116/REC-VOTable-1.5.html
                    var votable = await wc.DownloadStringTaskAsync($"https://simbad.cds.unistra.fr/simbad/sim-id?ident={edt2.Text}&coodisp1=d&obj.coo3=off&obj.coo4=off&obj.bibsel=off&obj.messel=off&obj.notesel=off&output.format=votable");
                    
                    // load XML document
                    doc.LoadXml(votable);

                    // parse VOTABLE/XML data
                    var FirstTable = doc.SelectSingleNode($"{XPathBase}/*[local-name() = 'TABLE']");
                    var fields = FirstTable?.SelectNodes("*[local-name() = 'FIELD']")?.Cast<XmlElement>().ToList();
                    var TableData = FirstTable?.SelectSingleNode("//*[local-name() = 'DATA']/*[local-name() = 'TABLEDATA']");
                    var rows = TableData?.SelectNodes("*[local-name() = 'TR']")?.Cast<XmlElement>().ToList();
                    var cols = rows[0]?.SelectNodes("*[local-name() = 'TD']")?.Cast<XmlElement>().ToList();

                    // get the TYPE_ID field and it's value
                    var fnv = GetFieldNameAndValue(fields, cols, "TYPED_ID");
                    txv1.Append($"{fnv.name} = {fnv.value}{SysEnv.NewLine}{SysEnv.NewLine}");

                    // get additional star parameters
                    var star_ra = GetFieldNameAndValue(fields, cols, "RA_d");
                    var star_de = GetFieldNameAndValue(fields, cols, "DEC_d");
                    var star_pmra = GetFieldNameAndValue(fields, cols, "PMRA");
                    var star_pmde = GetFieldNameAndValue(fields, cols, "PMDEC");
                    var star_parallax = GetFieldNameAndValue(fields, cols, "PLX_VALUE");
                    var star_radvel = GetFieldNameAndValue(fields, cols, "RV_VALUE");

                    // calculate the current postion of the star
                    var rigel = new Star(al) {
                        Number = 24436,
                        Catalog = "HIP",
                        Name = edt2.Text,
                        RightAscension = Convert.ToDouble(star_ra.value, CultureInfo.InvariantCulture) / 15.0,
                        Declination = Convert.ToDouble(star_de.value, CultureInfo.InvariantCulture),
                        DeltaT = AstroUtilities.DeltaT(jdUTC),
                        Parallax = Convert.ToDouble(star_parallax.value, CultureInfo.InvariantCulture),
                        ProperMotionRA = Convert.ToDouble(star_pmra.value, CultureInfo.InvariantCulture),
                        ProperMotionDec = Convert.ToDouble(star_pmde.value, CultureInfo.InvariantCulture),
                        RadialVelocity = Convert.ToDouble(star_radvel.value, CultureInfo.InvariantCulture)
                    };
                    var tcp = rigel.GetTopocentricPosition(jdTT, site, true);
                    txv1.Append($"{SysEnv.NewLine}{edt2.Text} topocentric RA: {Utilities.HoursToHMS(tcp.RightAscension)}{SysEnv.NewLine}");
                    txv1.Append($"{edt2.Text} topocentric DE: {Utilities.DegreesToDMS(tcp.Declination, "°", "′", "″", 0)}{SysEnv.NewLine}");
                }
                catch (Exception ex) {
                    txv1.Append($"Error: {ex.Message}{SysEnv.NewLine}");
                    SysTrace.WriteLine(ex.ToString());
                }
            };
        }

        private (string name, string value) GetFieldNameAndValue(List<XmlElement> fields, List<XmlElement> cols, string ID) {
            var fld = fields.FirstOrDefault(_ => _.GetAttribute("name") == ID);
            var ord = fields.IndexOf(fld);
            return (fld.GetAttribute("name"), cols[ord].InnerText);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults) {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        public void OnLocationChanged(Android.Locations.Location location) {
            longitude = location.Longitude;
            latitude = location.Latitude;
            if (!LocationIsKnown) LocationIsKnown = true;
        }

        public void OnProviderDisabled(string provider) {
            //throw new NotImplementedException();
        }

        public void OnProviderEnabled(string provider) {
            //throw new NotImplementedException();
        }

        public void OnStatusChanged(string? provider, [GeneratedEnum] Availability status, Bundle? extras) {
            //throw new NotImplementedException();
        }

        protected override void OnPause() {
            base.OnPause();
            lm?.RemoveUpdates(this);
        }

        protected override void OnResume() {
            base.OnResume();
            if (lm == null) return;

            if (lm.IsProviderEnabled(LocationManager.GpsProvider)) lm.RequestLocationUpdates(LocationManager.GpsProvider, 1000L, 1.0f, this);
            else if (lm.IsProviderEnabled(LocationManager.NetworkProvider)) lm.RequestLocationUpdates(LocationManager.NetworkProvider, 1000L, 1.0f, this);
            else Toast.MakeText(this, "Loactaion provider is disabled! Please enable it.", ToastLength.Short)?.Show();
        }

        // our custom Ascom Library logger class that writes logs directly to the screen console (textview)
        internal class AndroidLogger : ILogger {
            private AppCompatTextView _tv;

            public AndroidLogger(AppCompatTextView tv) {
                _tv = tv;
            }

            public LogLevel LoggingLevel {
                get;
                private set;
            } = LogLevel.Information;

            public void Log(LogLevel level, string message) {
                _tv?.Append($"{message}{SysEnv.NewLine}");
                switch (level) {
                    case LogLevel.Verbose:
                        Android.Util.Log.Verbose("DOTNET", message);
                        break;
                    case LogLevel.Debug:
                        Android.Util.Log.Debug("DOTNET", message);
                        break;
                    case LogLevel.Information:
                        Android.Util.Log.Info("DOTNET", message);
                        break;
                    case LogLevel.Warning:
                        Android.Util.Log.Warn("DOTNET", message);
                        break;
                    case LogLevel.Error:
                        Android.Util.Log.Error("DOTNET", message);
                        break;
                    case LogLevel.Fatal:
                        Android.Util.Log.Wtf("DOTNET", message);
                        break;
                }
            }

            public void SetMinimumLoggingLevel(LogLevel level) {
                LoggingLevel = level;
            }
        }
    }
}