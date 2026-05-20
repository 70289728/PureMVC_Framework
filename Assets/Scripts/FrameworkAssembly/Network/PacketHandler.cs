using System;
using System.Collections.Generic;

/// <summary>
/// Handles TCP packet framing and reassembly.
/// Packet format: [4-byte msgId (big-endian)][4-byte bodyLength (big-endian)][body bytes]
/// Correctly handles split packets and sticky packets (multiple packets in one read).
/// </summary>
public class PacketHandler
{
    private const int HEADER_SIZE = 8; // 4 bytes msgId + 4 bytes bodyLength

    private readonly List<byte> buffer = new List<byte>();

    /// <summary>
    /// Appends raw received bytes into the internal buffer.
    /// </summary>
    public void Append(byte[] data, int length)
    {
        for (int i = 0; i < length; i++)
            buffer.Add(data[i]);
    }

    /// <summary>
    /// Tries to read one complete packet from the buffer.
    /// Returns true and sets msgId/body when a full packet is available.
    /// Removes the consumed bytes from the buffer.
    /// </summary>
    public bool TryReadPacket(out int msgId, out byte[] body)
    {
        msgId = 0;
        body  = null;

        if (buffer.Count < HEADER_SIZE)
            return false;

        int id  = ReadInt32BigEndian(0);
        int len = ReadInt32BigEndian(4);

        if (len < 0 || buffer.Count < HEADER_SIZE + len)
            return false;

        msgId = id;
        body  = new byte[len];
        buffer.CopyTo(HEADER_SIZE, body, 0, len);
        buffer.RemoveRange(0, HEADER_SIZE + len);
        return true;
    }

    /// <summary>
    /// Clears the internal buffer (call on disconnect).
    /// </summary>
    public void Clear()
    {
        buffer.Clear();
    }

    private int ReadInt32BigEndian(int offset)
    {
        return (buffer[offset]     << 24) |
               (buffer[offset + 1] << 16) |
               (buffer[offset + 2] << 8)  |
                buffer[offset + 3];
    }
}
