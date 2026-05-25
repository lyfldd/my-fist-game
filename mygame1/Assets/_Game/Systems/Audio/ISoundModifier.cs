using _Game.Core;

namespace _Game.Systems.Audio
{
    /// <summary>
    /// 声音修改器接口。实现此接口并注册到 DecibelSystem，
    /// 所有声音发出前会依次经过修改器管道。
    /// </summary>
    public interface ISoundModifier
    {
        NoiseEvent Modify(NoiseEvent original);
    }
}
