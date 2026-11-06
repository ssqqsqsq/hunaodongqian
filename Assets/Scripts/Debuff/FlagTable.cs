// flag表：文档约定用 int[] 表示，1 = true，0 = false（不用 bool）
// 序号 1..5 对应 Duff.json 里的 [1, flagNum]，[0] 占位不用
// 写入方是突发事件系统，Debuff 判定只读取
public class FlagTable
{
    public const int MinIndex = 1;
    public const int MaxIndex = 5;

    readonly int[] flags = new int[MaxIndex + 1];

    public static bool IsValidIndex(int n)
    {
        return n >= MinIndex && n <= MaxIndex;
    }

    public bool IsTrue(int n)
    {
        return IsValidIndex(n) && flags[n] != 0;
    }

    public int GetValue(int n)
    {
        return IsValidIndex(n) ? flags[n] : 0;
    }

    public bool Set(int n, bool value)
    {
        if (!IsValidIndex(n)) return false;
        flags[n] = value ? 1 : 0;
        return true;
    }

    public void ClearAll()
    {
        for (int i = MinIndex; i <= MaxIndex; i++)
            flags[i] = 0;
    }

    // 调试用，输出形如 "[1]=0 [2]=1 ... [5]=0"
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = MinIndex; i <= MaxIndex; i++)
            sb.Append('[').Append(i).Append("]=").Append(flags[i]).Append(' ');
        return sb.ToString().TrimEnd();
    }
}
