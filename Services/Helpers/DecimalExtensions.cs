using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Helpers
{
    public static class DecimalExtensions
    {
        public static int ToInt(this decimal value) => (int)Math.Round(value);
    }
}
