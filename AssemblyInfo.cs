using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Android;
using Android.App;
using Android.Content.PM;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("AscomLibTest")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("F. Hoffmann La Roche Ltd")]
[assembly: AssemblyProduct("AscomLibTest")]
[assembly: AssemblyCopyright("Copyright © F. Hoffmann La Roche Ltd 2025")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM.

[assembly: Guid("d2dc5405-1ac0-489b-ae44-f54f4e8b8111")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

[assembly: UsesPermission(Manifest.Permission.Internet)]
[assembly: UsesPermission(Manifest.Permission.ReadExternalStorage)]
[assembly: UsesPermission(Manifest.Permission.AccessFineLocation)]
[assembly: UsesPermission(Manifest.Permission.AccessCoarseLocation)]

[assembly: UsesFeature(PackageManager.FeatureLocation, Required = true)]
