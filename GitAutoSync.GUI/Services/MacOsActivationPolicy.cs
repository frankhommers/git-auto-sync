using System.Runtime.InteropServices;

namespace GitAutoSync.GUI.Services;

internal static class MacOsActivationPolicy
{
  private const int NSApplicationActivationPolicyRegular = 0;
  private const int NSApplicationActivationPolicyAccessory = 1;

  public static void SetRegular()
  {
    if (!OperatingSystem.IsMacOS())
    {
      return;
    }

    SetActivationPolicy(NSApplicationActivationPolicyRegular);
  }

  public static void SetAccessory()
  {
    if (!OperatingSystem.IsMacOS())
    {
      return;
    }

    SetActivationPolicy(NSApplicationActivationPolicyAccessory);
  }

  private static void SetActivationPolicy(int policy)
  {
    try
    {
      IntPtr nsApplicationClass = objc_getClass("NSApplication");
      IntPtr sharedApplicationSel = sel_registerName("sharedApplication");
      IntPtr app = objc_msgSend(nsApplicationClass, sharedApplicationSel);

      IntPtr setActivationPolicySel = sel_registerName("setActivationPolicy:");
      objc_msgSend_bool_int(app, setActivationPolicySel, policy);
    }
    catch
    {
      // Ignore: if this fails we keep default dock behavior.
    }
  }

  [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
  private static extern IntPtr objc_getClass(string name);

  [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
  private static extern IntPtr sel_registerName(string selectorName);

  [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
  private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

  [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
  private static extern bool objc_msgSend_bool_int(IntPtr receiver, IntPtr selector, int arg1);
}