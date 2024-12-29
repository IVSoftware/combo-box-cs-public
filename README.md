As I mentioned in a comment, your answer is quite elegant; I like it a lot ▲. This supplementary answer shouldn't in any way diminish what you've accomplished; it's just to take a closer look at those unexplained events that occur in the native code after the `DropDown` event handler returns. If you didn't already know how to hook the `ListBox` messages (distinct from the `ComboBox` messages) this shows how.

___

Before posting my first answer, I had already tried the subclassing approach, and saw the same behavior on the `DropDown` event that you did, where "waiting for the method to return" by using `BeginInvoke` was insufficient because the Win32 message that causes the unwanted selection hasn't been added to the message queue yet, and that, sure, adding a "magic delay" to compensate does work. But I couldn't quite let it go without trying to ID a specific message and see what it would take to suppress that one msg.

What's tricky here is that this particular message cannot be observed in the `WndProc` of the `ComboBox` control or hooked in `IMessageFilter` because the `ListBox` portion has its own separate native control, with its own `hWnd` and `WndProc`. But since you're already making such good use of [P/Invoke](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke) we can go ahead and use that to get the native handle for the list box.

___

**Get ListBox Native HWND**
~~~

private const uint CB_GETCOMBOBOXINFO = 0x0164;
[StructLayout(LayoutKind.Sequential)]
private struct COMBOBOXINFO
{
    public int cbSize;
    public RECT rcItem;
    public RECT rcButton;
    public int stateButton;
    public IntPtr hwndCombo;
    public IntPtr hwndEdit;
    public IntPtr hwndList;
}

[StructLayout(LayoutKind.Sequential)]
private struct RECT
{
    public int left;
    public int top;
    public int right;
    public int bottom;
}

public static void GetNativeListBoxHandle(ComboBox comboBox, out IntPtr hwndList)
{
    COMBOBOXINFO comboBoxInfo = new COMBOBOXINFO();
    comboBoxInfo.cbSize = Marshal.SizeOf(comboBoxInfo);
    IntPtr comboBoxHandle = comboBox.Handle;
    IntPtr result = SendMessage(comboBoxHandle, CB_GETCOMBOBOXINFO, IntPtr.Zero, ref comboBoxInfo);
    if (result == IntPtr.Zero)
    {
        throw new InvalidOperationException("Failed to retrieve ComboBox information.");
    }
    hwndList = comboBoxInfo.hwndList;
}
~~~

___

**Monitor Listbox Win32 Messages**

Using the retrieved handle, we can hook into its `WndProc` like this and start being able to see messages like `LB_GETCURSEL` and `LB_SETCURSEL` for example that we couldn't see otherwise. The message that I got traction with was `LB_SETTOPINDEX`. Although ostensibly it doesn't select anything on its own, by suppressing this event I could prevent _any_ selection from occurring and fire an event to make something else happen instead.

~~~
private class ListBoxNativeWindow : NativeWindow
{
    public ListBoxNativeWindow(IntPtr handle) => AssignHandle(handle);
    protected override void WndProc(ref Message m)
    {
        var e = new CancelMessageEventArgs(m);
        switch ((WindowsMessages)m.Msg)
        {
            case WindowsMessages.LB_SETTOPINDEX:
                LB_SETTOPINDEX?.Invoke(this, e);
                break;
            default:
                break;
        }
        if (e.Cancel)
        {
            m.Result = 1;
        }
        else
        {
            base.WndProc(ref m);
        }
    }
    public event EventHandler<CancelMessageEventArgs>? LB_SETTOPINDEX;
}
~~~

___

**Alternative Approach, Leveraging LB Messages**

Obviously, this has the potential clear up most of the mystery that surrounds what's happening in the native LB. Here's one thing I tried, that seemed fairly effective in meeting your spec, at least as I understand it.

~~~
public class ComboBoxCS : ComboBox
{
    IntPtr _hwndListBox;
    ListBoxNativeWindow? _listBoxNativeWindow { get; set; }
    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        GetNativeListBoxHandle(this, out _hwndListBox);
        _listBoxNativeWindow = new ListBoxNativeWindow(_hwndListBox);
        _listBoxNativeWindow.LB_SETTOPINDEX += (sender, e) =>
        {
            if(DroppedDown)
            {
                if (CaseSensitiveMatchIndex != -1)
                {
                    if (SelectedIndex != CaseSensitiveMatchIndex)
                    {
                        LockWindowUpdate(this.Handle);
                        e.Cancel = true;
                        BeginInvoke(() =>
                        {
                            SelectedIndex = CaseSensitiveMatchIndex;
                            LockWindowUpdate(IntPtr.Zero);
                        });
                    }
                }
            }
        };
    }
    Keys _key = Keys.None;
    private int _selectionStartB4;
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _key = e.KeyData;
        if (_key == Keys.Return)
        {
            BeginInvoke(() => SelectAll());
        }
        else
        {
            // Capture, e.g. "pre-backspace"
            _selectionStartB4 = SelectionStart;
        }
    }
    protected override void OnSelectionChangeCommitted(EventArgs e)
    {
        base.OnSelectionChangeCommitted(e);
        CaseSensitiveMatchIndex = SelectedIndex;
    }
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        var captureKey = _key;
        _key = Keys.None;
        if (captureKey == Keys.None)
        {
            // Text is changing programmatically.
            // Do not recalculate auto-complete here.

            // This next block fixes an artifact of drop closing without committing the selection.
            if (CaseSensitiveMatchIndex != -1 )
            {
                var sbText = Items[CaseSensitiveMatchIndex]?.ToString() ?? String.Empty;
                Debug.WriteLine($"{CaseSensitiveMatchIndex} is:{Text} sb:{sbText}");
                if(Text != sbText)
                {
                    BeginInvoke(() => Text = sbText);
                }
            }
        }
        else
        {
            BeginInvoke(() =>
            {
                if (captureKey == Keys.Back)
                {
                    SelectionStart = Math.Max(0, _selectionStartB4 - 1);
                    if(SelectionStart == 0) // Backspaced to the start
                    {
                        BeginInvoke(() =>
                        {
                            CaseSensitiveMatchIndex = -1;
                            Text = string.Empty;
                        });
                        return;
                    }
                }
                var substr = Text.Substring(0, SelectionStart);
                if (string.IsNullOrEmpty(substr))
                {
                    CaseSensitiveMatchIndex = -1;
                }
                else
                {
                    Debug.WriteLine(substr);
                    int i;
                    for (i = 0; i < Items.Count; i++)
                    {
                        if ((Items[i]?.ToString() ?? string.Empty).StartsWith(substr))
                        {
                            SelectIndexAndRestoreCursor(i);
                            break;
                        }
                    }
                    CaseSensitiveMatchIndex = i == Items.Count ? -1 : i;
                }
            });
        }
    }
    void SelectIndexAndRestoreCursor(int index)
    {
        BeginInvoke(() =>
        {
            var selStartB4 = SelectionStart;
            SelectedIndex = index;
            SelectionStart = selStartB4;
            SelectionLength = Text.Length - SelectionStart;
        });
    }

    public int CaseSensitiveMatchIndex { get; private set; } = -1;

    #region P I N V O K E
    ...
    #endregion P I N V O K E
}

class CancelMessageEventArgs : CancelEventArgs
{
    public CancelMessageEventArgs(Message message) => Message = message;

    public Message Message { get; }
}
~~~