namespace AdaptiveLayout
{
    internal interface ISafeAreaSource
    {
        bool TryGetSnapshot(out SafeAreaSnapshot snapshot);
    }
}
