// using System.Text;

// public static class Extensions
// {
//     // string -> ReadOnlyMemory<byte>
//     public static ReadOnlyMemory<byte> ToReadOnlyMemory(this string str)
//     {
//         return Encoding.UTF8.GetBytes(str);
//     }

//     // string -> ReadOnlySpan<byte>
//     public static ReadOnlySpan<byte> ToReadOnlySpan(this string str)
//     {
//         return Encoding.UTF8.GetBytes(str);
//     }

//     // ReadOnlyMemory<byte> -> string
//     public static string ToString(this ReadOnlyMemory<byte> memory)
//     {
//         return Encoding.UTF8.GetString(memory.Span);
//     }

//     // ReadOnlyMemory<byte> -> ReadOnlySpan<byte>
//     public static ReadOnlySpan<byte> ToReadOnlySpan(this ReadOnlyMemory<byte> memory)
//     {
//         return memory.Span;
//     }
// }