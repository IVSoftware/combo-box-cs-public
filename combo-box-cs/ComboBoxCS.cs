using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace combo_box_cs
{
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
                // Do not recalculate auto-complete here..
                // This fixes an artifact of drop closing without committing the selection.
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
        [DllImport("user32")]
        public static extern bool LockWindowUpdate(IntPtr hWndLock);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, ref COMBOBOXINFO lParam);

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
        #endregion P I N V O K E
    }
    class CancelMessageEventArgs : CancelEventArgs
    {
        public CancelMessageEventArgs(Message message) => Message = message;

        public Message Message { get; }
    }
}
