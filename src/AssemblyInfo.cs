using System.Reflection;
using System.Runtime.InteropServices;

// Embeds a real product name / publisher / version into the exe so Windows does
// not see an anonymous unsigned binary. This mirrors the sibling tool's
// version_info.txt (it is metadata, not an Authenticode code signature). csc
// compiles these attributes into the Win32 version resource.
[assembly: AssemblyTitle("Angels Online FixRes")]            // FileDescription
[assembly: AssemblyProduct("Angels Online FixRes")]          // ProductName
[assembly: AssemblyCompany("nosorry")]                       // CompanyName
[assembly: AssemblyCopyright("Copyright (c) 2026 nosorry")]  // LegalCopyright
[assembly: AssemblyDescription("Fixes blurry rendering, crashes and black bars in Angels Online Global.")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]                   // FileVersion
[assembly: AssemblyInformationalVersion("1.1.0.0")]          // ProductVersion
[assembly: ComVisible(false)]
