using System;

namespace AutoExpr
{
    public static class Statistics
    {
        public static (double x, double y) BoxMuller(double u, double v)
        {
            var x = Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(2.0 * Math.PI * v);
	        var y = Math.Sqrt(-2.0 * Math.Log(u)) * Math.Sin(2.0 * Math.PI * v);

            return (x,y);
        }
    }
}