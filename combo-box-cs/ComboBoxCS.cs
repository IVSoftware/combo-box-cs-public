﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace combo_box_cs
{
    public class ComboBoxCS : ComboBox
    {
        IntPtr _hwndEdit, _hwndCombo, _hwndList;
        ListNativeWindow? _listNativeWindow { get; set; }
        EditNativeWindow? _editNativeWindow { get; set; }
        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            GetNativeHandles(this, out _hwndCombo, out _hwndEdit, out _hwndList);
            _listNativeWindow = new ListNativeWindow(_hwndList);
            _editNativeWindow = new EditNativeWindow(_hwndEdit);
            _listNativeWindow.LB_SETTOPINDEX += (sender, e) =>
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
            _editNativeWindow.WM_SETTEXT += (sender, e) =>
            {
                var aspirant = e.Message.ExtractTextPayload();
                if (CaseSensitiveMatchIndex != -1)
                {
                    var sbText = Items[CaseSensitiveMatchIndex]?.ToString() ?? String.Empty;
                    Debug.WriteLine($"{CaseSensitiveMatchIndex} proposed:{aspirant} sb:{sbText}");
                    if (aspirant != sbText)
                    {
                        e.Cancel= true;
                        BeginInvoke(() =>
                        {
                            Text = sbText;
                        });
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

        public static void GetNativeHandles(ComboBox comboBox, out IntPtr hwndCombo, out IntPtr hwndEdit, out IntPtr hwndList)
        {
            COMBOBOXINFO comboBoxInfo = new COMBOBOXINFO();
            comboBoxInfo.cbSize = Marshal.SizeOf(comboBoxInfo);
            IntPtr comboBoxHandle = comboBox.Handle;
            IntPtr result = SendMessage(comboBoxHandle, CB_GETCOMBOBOXINFO, IntPtr.Zero, ref comboBoxInfo);
            if (result == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to retrieve ComboBox information.");
            }
            hwndCombo = comboBoxInfo.hwndCombo;
            hwndEdit = comboBoxInfo.hwndEdit;
            hwndList = comboBoxInfo.hwndList;
        }
        private class ListNativeWindow : NativeWindow
        {
            public ListNativeWindow(IntPtr handle) => AssignHandle(handle);
            protected override void WndProc(ref Message m)
            {
                var desc = $"{(WindowsMessages)m.Msg}";
                Debug.WriteLineIf(false, $"LIST: {desc}");
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
        private class EditNativeWindow : NativeWindow
        {
            public EditNativeWindow(IntPtr handle) => AssignHandle(handle);
            protected override void WndProc(ref Message m)
            {
                var desc = $"{(WindowsMessages)m.Msg}";
                Debug.WriteLineIf(false, $"EDIT: {desc}");
                var e = new CancelMessageEventArgs(m);
                switch ((WindowsMessages)m.Msg)
                {
                    case WindowsMessages.WM_SETTEXT:
                        Debug.WriteLine($"EDIT {desc} value: {m.ExtractTextPayload()}");
                        WM_SETTEXT?.Invoke(this, e);
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
                    switch ((WindowsMessages)m.Msg)
                    {
                        case WindowsMessages.WM_GETTEXT:
                            Debug.WriteLineIf(false, $"EDIT {desc} value: {m.ExtractTextPayload()}");
                            break;
                        default:
                            break;
                    }
                }
            }
            public event EventHandler<CancelMessageEventArgs>? WM_SETTEXT;
        }
        #endregion P I N V O K E
    }
    class CancelMessageEventArgs : CancelEventArgs
    {
        public CancelMessageEventArgs(Message message) => Message = message;

        public Message Message { get; }
    }
    static class Extensions
    {
        public static string? ExtractTextPayload(this Message msg)
        {
            IntPtr textBuffer = msg.LParam;
            if (textBuffer != IntPtr.Zero)
            {
                return Marshal.PtrToStringUni(textBuffer) ?? string.Empty;
            }
            else return null;
        }
    }
}
