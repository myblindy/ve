using System;
using System.Collections.Generic;
using System.Text;

namespace ve.Model
{
    public interface IKeyFrameInterpolation<T>
    {
        T InterpolateWith(T other, double ratio);
    }
}
