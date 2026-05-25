using _Game.Core;

namespace _Game.Systems.Audio
{
    /// <summary>
    /// 声音监听者接口。实现此接口并注册到 DecibelSystem，
    /// 每当有声音发出时会收到回调。
    /// </summary>
    public interface ISoundListener
    {
        void OnSoundHeard(NoiseEvent noise);
    }
}
