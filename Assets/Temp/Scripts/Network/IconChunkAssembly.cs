using System;

/// <summary>
/// 分けて送られてきたアイコンの PNG を 1 つにまとめる。
/// 新しい送信番号の断片が来たら、それまでの途中のものは捨てる。最大の大きさを超えるものは受け付けない。
/// </summary>
public class IconChunkAssembly
{
    readonly int maxBytes;
    readonly int maxChunks;
    byte[][] chunks;
    int uploadId;
    int receivedCount;
    int totalBytes;

    public IconChunkAssembly(int maxBytes, int maxChunks)
    {
        this.maxBytes = maxBytes;
        this.maxChunks = maxChunks;
    }

    /// <summary>断片を 1 つ受け取る。全部そろったらまとめた PNG を返し、まだ・不正なら null を返す</summary>
    public byte[] Add(int id, int index, int count, byte[] data)
    {
        if (count <= 0 || count > maxChunks || index < 0 || index >= count || data == null || data.Length == 0)
            return null;

        if (chunks == null || id != uploadId || chunks.Length != count)
            Begin(id, count);

        if (chunks[index] != null)
            return null;

        totalBytes += data.Length;
        if (totalBytes > maxBytes)
        {
            Reset();
            return null;
        }

        chunks[index] = data;
        receivedCount++;
        return receivedCount == count ? Combine() : null;
    }

    void Begin(int id, int count)
    {
        uploadId = id;
        chunks = new byte[count][];
        receivedCount = 0;
        totalBytes = 0;
    }

    void Reset()
    {
        chunks = null;
        receivedCount = 0;
        totalBytes = 0;
    }

    byte[] Combine()
    {
        var result = new byte[totalBytes];
        int offset = 0;
        foreach (byte[] chunk in chunks)
        {
            Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length);
            offset += chunk.Length;
        }

        Reset();
        return result;
    }
}
