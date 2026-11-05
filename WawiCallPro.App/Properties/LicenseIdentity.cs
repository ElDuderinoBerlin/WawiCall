using System.Runtime.InteropServices;
using WawiCallPro.Utilities.Licensing;

#if LX_PRO
[assembly: Guid(LicenseProductIdentity.ProApplicationId)]
#else
[assembly: Guid(LicenseProductIdentity.FinOfficeApplicationId)]
#endif
