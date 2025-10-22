# TimeTrack Namespace Update Guide

## ? Completed Updates

### 1. **App.xaml.cs** ?
- Added `using TimeTrack.Views;`
- Namespace: `TimeTrack` (stays at root)

### 2. **Views\MainWindow.xaml** ?
- Updated `x:Class="TimeTrack.Views.MainWindow"`
- All resource references working

### 3. **Views\MainWindow.xaml.cs** ?
- Updated namespace to `TimeTrack.Views`

### 4. **Utilities\SettingsManager.cs** ?
- Updated namespace to `TimeTrack.Utilities`
- Removed circular `using TimeTrack.Utilities;`

---

## ?? Dialog Windows - Manual Updates Required

For **EACH** of the following dialog window files, update as shown:

### **Views\Dialogs\AboutWindow.xaml**

```xaml
<!-- Line 1 - UPDATE THIS: -->
<Window x:Class="TimeTrack.Views.Dialogs.AboutWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        ...>
```

### **Views\Dialogs\AboutWindow.xaml.cs**

```csharp
// UPDATE NAMESPACE:
using System.Windows;
using TimeTrack.Utilities;  // Add if needed

namespace TimeTrack.Views.Dialogs
{
    public partial class AboutWindow : Window
    {
        // ...existing code...
    }
}
```

---

### **Views\Dialogs\EditEntryWindow.xaml**

```xaml
<!-- Line 1 - UPDATE THIS: -->
<Window x:Class="TimeTrack.Views.Dialogs.EditEntryWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        ...>
```

### **Views\Dialogs\EditEntryWindow.xaml.cs**

```csharp
// UPDATE NAMESPACE:
using System;
using System.Windows;
using TimeTrack.Data;
using TimeTrack.Utilities;

namespace TimeTrack.Views.Dialogs
{
    public partial class EditEntryWindow : Window
    {
        // ...existing code...
    }
}
```

---

### **Views\Dialogs\NotesEditorWindow.xaml**

```xaml
<!-- Line 1 - UPDATE THIS: -->
<Window x:Class="TimeTrack.Views.Dialogs.NotesEditorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        ...>
```

### **Views\Dialogs\NotesEditorWindow.xaml.cs**

```csharp
// UPDATE NAMESPACE:
using System.Windows;

namespace TimeTrack.Views.Dialogs
{
    public partial class NotesEditorWindow : Window
    {
        // ...existing code...
    }
}
```

---

### **Views\Dialogs\OptionsWindow.xaml**

```xaml
<!-- Line 1 - UPDATE THIS: -->
<Window x:Class="TimeTrack.Views.Dialogs.OptionsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        ...>
```

### **Views\Dialogs\OptionsWindow.xaml.cs**

```csharp
// UPDATE NAMESPACE:
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using TimeTrack.Utilities;

namespace TimeTrack.Views.Dialogs
{
    public partial class OptionsWindow : Window
    {
        // ...existing code...
    }
}
```

---

### **Views\Dialogs\ShortcutInputDialog.xaml**

```xaml
<!-- Line 1 - UPDATE THIS: -->
<Window x:Class="TimeTrack.Views.Dialogs.ShortcutInputDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        ...>
```

### **Views\Dialogs\ShortcutInputDialog.xaml.cs**

```csharp
// UPDATE NAMESPACE:
using System.Windows;
using System.Windows.Input;

namespace TimeTrack.Views.Dialogs
{
    public partial class ShortcutInputDialog : Window
    {
        // ...existing code...
    }
}
```

---

## ?? Update Views\MainWindow.xaml.cs References

In `Views\MainWindow.xaml.cs`, add these using statements at the top:

```csharp
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TimeTrack.Data;
using TimeTrack.ViewModels;
using TimeTrack.Utilities;
using TimeTrack.Views.Dialogs;  // ? ADD THIS LINE

namespace TimeTrack.Views
{
    public partial class MainWindow : Window
    {
        // ...existing code...
    }
}
```

---

## ?? Quick Find & Replace

In **Visual Studio**, use Find & Replace (Ctrl+H) for each dialog:

### For AboutWindow:
- **Find:** `x:Class="TimeTrack.AboutWindow"`
- **Replace:** `x:Class="TimeTrack.Views.Dialogs.AboutWindow"`

### For EditEntryWindow:
- **Find:** `x:Class="TimeTrack.EditEntryWindow"`
- **Replace:** `x:Class="TimeTrack.Views.Dialogs.EditEntryWindow"`

### For NotesEditorWindow:
- **Find:** `x:Class="TimeTrack.NotesEditorWindow"`
- **Replace:** `x:Class="TimeTrack.Views.Dialogs.NotesEditorWindow"`

### For OptionsWindow:
- **Find:** `x:Class="TimeTrack.OptionsWindow"`
- **Replace:** `x:Class="TimeTrack.Views.Dialogs.OptionsWindow"`

### For ShortcutInputDialog:
- **Find:** `x:Class="TimeTrack.ShortcutInputDialog"`
- **Replace:** `x:Class="TimeTrack.Views.Dialogs.ShortcutInputDialog"`

---

## ? Final Structure

```
TimeTrack/
??? App.xaml (namespace: TimeTrack)
??? App.xaml.cs (namespace: TimeTrack)
??? Views/
?   ??? MainWindow.xaml (namespace: TimeTrack.Views)
?   ??? MainWindow.xaml.cs (namespace: TimeTrack.Views)
?   ??? Dialogs/
?       ??? AboutWindow.xaml (namespace: TimeTrack.Views.Dialogs)
?       ??? AboutWindow.xaml.cs (namespace: TimeTrack.Views.Dialogs)
?       ??? EditEntryWindow.xaml (namespace: TimeTrack.Views.Dialogs)
?       ??? EditEntryWindow.xaml.cs (namespace: TimeTrack.Views.Dialogs)
?       ??? NotesEditorWindow.xaml (namespace: TimeTrack.Views.Dialogs)
?       ??? NotesEditorWindow.xaml.cs (namespace: TimeTrack.Views.Dialogs)
?       ??? OptionsWindow.xaml (namespace: TimeTrack.Views.Dialogs)
?       ??? OptionsWindow.xaml.cs (namespace: TimeTrack.Views.Dialogs)
?       ??? ShortcutInputDialog.xaml (namespace: TimeTrack.Views.Dialogs)
?       ??? ShortcutInputDialog.xaml.cs (namespace: TimeTrack.Views.Dialogs)
??? ViewModels/
?   ??? TimeKeeperViewModel.cs (namespace: TimeTrack.ViewModels)
??? Data/
?   ??? Database.cs (namespace: TimeTrack.Data)
?   ??? TimeTrackDbContext.cs (namespace: TimeTrack.Data)
??? Utilities/
?   ??? ErrorHandler.cs (namespace: TimeTrack.Utilities)
?   ??? SettingsManager.cs (namespace: TimeTrack.Utilities) ?
?   ??? TimeStringConverter.cs (namespace: TimeTrack.Utilities)
??? Resources/
    ??? [resource files]
```

---

## ?? Build & Test

After making all changes:

1. **Clean Solution** (Ctrl+Shift+B, then Build > Clean Solution)
2. **Rebuild Solution** (Ctrl+Shift+B)
3. **Fix any remaining errors**
4. **Run the application**
5. **Test all windows open correctly**

---

## ?? Common Issues

### Issue: "Type not found" errors
**Solution:** Make sure you added `using TimeTrack.Views.Dialogs;` to MainWindow.xaml.cs

### Issue: XAML designer errors
**Solution:** Rebuild the solution - designer updates after successful build

### Issue: "Cannot find type in namespace"
**Solution:** Double-check that x:Class matches the namespace in .xaml.cs file exactly

---

**Created:** 2025-01-XX  
**Status:** Ready for implementation  
**Estimated Time:** 10-15 minutes
