// netstandard2.1 には System.Runtime.CompilerServices.IsExternalInit が無い。
// C# 9 のコンパイラは init アクセサと positional record を出力するのにこの型を要求するため、
// internal で宣言して Containers.Runtime の中だけで使えるようにしている。
// internal なので、同じプロジェクトの他アセンブリが同名の型を持っていても衝突しない。
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
