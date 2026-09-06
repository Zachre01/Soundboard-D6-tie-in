using System.Runtime.InteropServices;

namespace DeckSoundboard;

public sealed class PttController
{
    private string? _heldKey;
    public bool IsHeld => _heldKey is not null;

    public bool Press(string keyText)
    {
        if (IsHeld) return true;
        if (!TryParseKey(keyText, out var code, out var mouseFlagDown, out _)) return false;
        if (mouseFlagDown != 0)
            SendMouse(mouseFlagDown, IsXButton(keyText) ? (keyText.Equals("Mouse5", StringComparison.OrdinalIgnoreCase) || keyText.Equals("XButton2", StringComparison.OrdinalIgnoreCase) ? 2u : 1u) : 0u);
        else
            SendKeyboard(code, false);
        _heldKey = keyText;
        return true;
    }

    public void Release()
    {
        if (_heldKey is null) return;
        var keyText = _heldKey;
        _heldKey = null;
        if (!TryParseKey(keyText, out var code, out _, out var mouseFlagUp)) return;
        if (mouseFlagUp != 0)
            SendMouse(mouseFlagUp, IsXButton(keyText) ? (keyText.Equals("Mouse5", StringComparison.OrdinalIgnoreCase) || keyText.Equals("XButton2", StringComparison.OrdinalIgnoreCase) ? 2u : 1u) : 0u);
        else
            SendKeyboard(code, true);
    }

    private static bool TryParseKey(string text, out ushort vk, out uint mouseDown, out uint mouseUp)
    {
        vk = 0; mouseDown = 0; mouseUp = 0;
        text = (text ?? "").Trim();
        if (text.Equals("Mouse4", StringComparison.OrdinalIgnoreCase) || text.Equals("XButton1", StringComparison.OrdinalIgnoreCase)) { mouseDown = 0x0080; mouseUp = 0x0100; return true; }
        if (text.Equals("Mouse5", StringComparison.OrdinalIgnoreCase) || text.Equals("XButton2", StringComparison.OrdinalIgnoreCase)) { mouseDown = 0x0080; mouseUp = 0x0100; return true; }
        if (text.Equals("LButton", StringComparison.OrdinalIgnoreCase) || text.Equals("Mouse1", StringComparison.OrdinalIgnoreCase)) { mouseDown = 0x0002; mouseUp = 0x0004; return true; }
        if (text.Equals("RButton", StringComparison.OrdinalIgnoreCase) || text.Equals("Mouse2", StringComparison.OrdinalIgnoreCase)) { mouseDown = 0x0008; mouseUp = 0x0010; return true; }
        if (text.Equals("MButton", StringComparison.OrdinalIgnoreCase) || text.Equals("Mouse3", StringComparison.OrdinalIgnoreCase)) { mouseDown = 0x0020; mouseUp = 0x0040; return true; }

        if (text.Length == 1)
        {
            var c = char.ToUpperInvariant(text[0]);
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) { vk = c; return true; }
        }
        if (text.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(text[1..], out var f) && f is >= 1 and <= 24) { vk = (ushort)(0x70 + f - 1); return true; }
        var named = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            ["Space"] = 0x20, ["Tab"] = 0x09, ["Enter"] = 0x0D, ["Shift"] = 0x10, ["Ctrl"] = 0x11, ["Control"] = 0x11,
            ["Alt"] = 0x12, ["CapsLock"] = 0x14, ["Escape"] = 0x1B, ["Esc"] = 0x1B, ["Insert"] = 0x2D, ["Delete"] = 0x2E,
            ["Home"] = 0x24, ["End"] = 0x23, ["PageUp"] = 0x21, ["PageDown"] = 0x22, ["Up"] = 0x26, ["Down"] = 0x28,
            ["Left"] = 0x25, ["Right"] = 0x27, ["Backspace"] = 0x08
        };
        return named.TryGetValue(text, out vk);
    }

    private static void SendKeyboard(ushort vk, bool keyUp)
    {
        INPUT[] inputs = { new() { type = 1, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = keyUp ? 0x0002u : 0 } } } };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private static bool IsXButton(string keyText) => keyText.Equals("Mouse4", StringComparison.OrdinalIgnoreCase) || keyText.Equals("XButton1", StringComparison.OrdinalIgnoreCase) || keyText.Equals("Mouse5", StringComparison.OrdinalIgnoreCase) || keyText.Equals("XButton2", StringComparison.OrdinalIgnoreCase);

    private static void SendMouse(uint flags, uint mouseData)
    {
        INPUT[] inputs = { new() { type = 0, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags, mouseData = mouseData } } } };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
