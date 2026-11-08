using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TwoZoneClock {
    // The edit field and its result list are a single native combo box.
    internal sealed class SearchableRegionCombo : ComboBox {
        private RegionChoice[] choices;
        private bool changing, assigningText, assigningIndex, composing;
        private string selectedId;
        private EditObserver edit;
        internal bool HasSelection { get; private set; }
        internal string SelectedRegionId { get { return selectedId; } }
        internal event EventHandler SelectionStateChanged;

        internal SearchableRegionCombo() {
            DropDownStyle = ComboBoxStyle.DropDown;
            AutoCompleteMode = AutoCompleteMode.None;
            IntegralHeight = false; MaxDropDownItems = 8; DropDownHeight = 200;
        }
        public override string Text {
            get { return base.Text; }
            set {
                if (changing || assigningIndex || choices == null) { base.Text = value; return; }
                assigningText = true;
                try { base.Text = value; } finally { assigningText = false; }
                Search(value ?? "", false);
            }
        }
        public override int SelectedIndex {
            get { return base.SelectedIndex; }
            set {
                assigningIndex = true;
                try { base.SelectedIndex = value; } finally { assigningIndex = false; }
                if (!changing && !assigningText) CommitSelection();
            }
        }
        internal void SetChoices(RegionChoice[] catalog, string id, bool preserveQuery) {
            string query = base.Text;
            bool pending = choices != null && !HasSelection && preserveQuery;
            choices = catalog; selectedId = id;
            if (pending) Search(query, false); else ShowChosen();
        }
        private void ShowChosen() {
            changing = true;
            BeginUpdate();
            try {
                Items.Clear(); Items.AddRange(choices);
                int chosen = -1;
                for (int i = 0; i < Items.Count; i++) if (((RegionChoice)Items[i]).Id == selectedId) { chosen = i; break; }
                base.SelectedIndex = chosen;
                HasSelection = chosen >= 0;
                if (chosen >= 0) base.Text = Items[chosen].ToString();
                SelectionStart = Focused ? 0 : base.Text.Length; SelectionLength = Focused ? base.Text.Length : 0;
            } finally { EndUpdate(); changing = false; }
            NotifyState();
        }
        private void Search(string query, bool openResults) {
            if (choices == null || changing || composing) return;
            int caret = Math.Min(SelectionStart, query.Length);
            int selectionLength = Math.Min(SelectionLength, query.Length - caret);
            changing = true;
            BeginUpdate();
            try {
                Items.Clear();
                foreach (RegionChoice choice in choices) if (choice.Matches(query)) Items.Add(choice);
                base.SelectedIndex = -1; HasSelection = false;
                base.Text = query;
                if (openResults && Visible && Focused) DroppedDown = Items.Count > 0;
                // Native list rebuilding can select or replace edit text; restore it exactly.
                base.SelectedIndex = -1; base.Text = query;
                SelectionStart = caret; SelectionLength = selectionLength;
            } finally { EndUpdate(); changing = false; }
            NotifyState();
        }
        private void CommitSelection() {
            if (changing || assigningText || composing || SelectedItem == null) return;
            selectedId = ((RegionChoice)SelectedItem).Id;
            HasSelection = true; NotifyState();
        }
        protected override void OnTextUpdate(EventArgs e) {
            base.OnTextUpdate(e);
            if (!changing && !assigningText && !composing) Search(base.Text, true);
        }
        protected override void OnSelectedIndexChanged(EventArgs e) {
            base.OnSelectedIndexChanged(e);
            if (!assigningIndex) CommitSelection();
        }
        protected override void OnSelectionChangeCommitted(EventArgs e) {
            CommitSelection(); base.OnSelectionChangeCommitted(e);
        }
        protected override void OnDropDown(EventArgs e) {
            if (!changing && HasSelection && choices != null) ShowChosen();
            base.OnDropDown(e);
        }
        protected override void OnEnter(EventArgs e) {
            base.OnEnter(e);
            if (HasSelection) { SelectionStart = 0; SelectionLength = base.Text.Length; }
        }
        protected override void OnKeyDown(KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter && !composing && DroppedDown && Items.Count > 0) {
                if (SelectedIndex < 0) SelectedIndex = 0;
                CommitSelection(); DroppedDown = false;
                e.Handled = true; e.SuppressKeyPress = true;
            } else if (e.KeyCode == Keys.Escape && !HasSelection && !composing) {
                ShowChosen(); DroppedDown = false;
                e.Handled = true; e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }
        private void NotifyState() { if (SelectionStateChanged != null) SelectionStateChanged(this, EventArgs.Empty); }

        // Composition messages belong to the combo's edit child, not the parent.
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct COMBOBOXINFO {
            public int cbSize; public RECT rcItem, rcButton; public uint stateButton;
            public IntPtr hwndCombo, hwndItem, hwndList;
        }
        [DllImport("user32.dll")] private static extern bool GetComboBoxInfo(IntPtr hwnd, ref COMBOBOXINFO info);
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            var info = new COMBOBOXINFO { cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO)) };
            if (GetComboBoxInfo(Handle, ref info) && info.hwndItem != IntPtr.Zero) edit = new EditObserver(this, info.hwndItem);
        }
        protected override void OnHandleDestroyed(EventArgs e) {
            if (edit != null) { edit.ReleaseHandle(); edit = null; }
            composing = false; base.OnHandleDestroyed(e);
        }
        private sealed class EditObserver : NativeWindow {
            private readonly SearchableRegionCombo owner;
            internal EditObserver(SearchableRegionCombo combo, IntPtr hwnd) { owner = combo; AssignHandle(hwnd); }
            protected override void WndProc(ref Message message) {
                if (message.Msg == 0x010D) owner.composing = true; // WM_IME_STARTCOMPOSITION
                base.WndProc(ref message);
                if (message.Msg == 0x0202 && owner.HasSelection && !owner.composing) owner.SelectAll(); // Replace the committed label on click.
                if (message.Msg == 0x010E) { // WM_IME_ENDCOMPOSITION
                    owner.composing = false;
                    if (!owner.IsDisposed && owner.IsHandleCreated) owner.BeginInvoke(new Action(delegate {
                        if (!owner.IsDisposed && !owner.composing) owner.Search(owner.Text, true);
                    }));
                }
            }
        }
    }
}
