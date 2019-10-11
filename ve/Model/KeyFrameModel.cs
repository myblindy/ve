using ReactiveUI;
using System;
using Avalonia.Media;
using System.Collections.ObjectModel;
using MoreLinq;

namespace ve.Model
{
    public class KeyFrameModel<T> where T : IKeyFrameInterpolation<T>
    {
        public ObservableCollection<KeyFrameInstanceModel<T>> KeyFrames { get; } = new ObservableCollection<KeyFrameInstanceModel<T>>();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1043:Use Integral Or String Argument For Indexers", Justification = "We're indexing time here.")]
        public T this[TimeSpan timestamp]
        {
            get
            {
                if (KeyFrames.Count == 0) return default;

                KeyFrameInstanceModel<T> prev = null;
                foreach (var kf in KeyFrames)
                {
                    if (kf.Timestamp > timestamp)
                        if (prev is null)
                            throw new InvalidOperationException();
                        else
                            return prev.InnerObject.InterpolateWith(kf.InnerObject, (timestamp.TotalSeconds - prev.Timestamp.TotalSeconds) / (kf.Timestamp.TotalSeconds - prev.Timestamp.TotalSeconds));
                    prev = kf;
                }

                return prev is null ? default : prev.InnerObject;
            }
        }

        public void AddKeyFrame(T instance, TimeSpan timestamp)
        {
            // figure out where to insert it
            int idx = 0;
            foreach (var kf in KeyFrames)
                if (kf.Timestamp > timestamp)
                {
                    KeyFrames.Insert(idx, new KeyFrameInstanceModel<T> { InnerObject = instance, Timestamp = timestamp });
                    break;
                }
                else
                    ++idx;

            // at the end then
            KeyFrames.Add(new KeyFrameInstanceModel<T> { InnerObject = instance, Timestamp = timestamp });
        }

        public void AddKeyFrame(TimeSpan timestamp) =>
            AddKeyFrame(this[timestamp], timestamp);
    }
}
