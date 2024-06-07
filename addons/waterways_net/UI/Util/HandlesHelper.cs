namespace Waterways.UI.Util;

public static class HandlesHelper
{
    /* 
    Handles are pushed to separate handle lists, one per material (using gizmo.add_handles).
	A handle's "index" is given (by Godot) in order it was added to a gizmo. 
	Given that N = points in the curve:
	- First we add the center ("actual") curve handles, therefore
	  the handle's index is the same as the curve point's index.
	- Then we add the in and out points together. So the first curve point's IN handle
	  gets an index of N. The OUT handle gets N+1.
	- Finally the left/right indices come last, and the first curve point's LEFT is N * 3 .
	  (3 because there are three rows before the left/right indices)
	
	Examples for N = 2, 3, 4:
	curve points 2:0   1      3:0   1   2        4:0   1   2   3
	------------------------------------------------------------------
	center         0   1        0   1   2          0   1   2   3
	in             2   4        3   5   7          4   6   8   10
	out            3   5        4   6   8          5   7   9   11
	left           6   8        9   11  13         12  14  16  18
	right          7   9        10  12  14         13  15  17  19
	
	The following utility functions calculate to and from curve/handle indices.
	*/

    public static bool IsCenterPoint(int index, int curvePointCount)
    {
        return index < curvePointCount;
    }

    public static bool IsControlPointIn(int index, int curvePointCount)
    {
        if (index < curvePointCount)
        {
            return false;
        }

        if (index >= curvePointCount * 3)
        {
            return false;
        }

        return (index - curvePointCount) % 2 == 0;
    }

    public static bool IsControlPointOut(int index, int curvePointCount)
    {
        if (index < curvePointCount)
        {
            return false;
        }

        if (index >= curvePointCount * 3)
        {
            return false;
        }

        return (index - curvePointCount) % 2 == 1;
    }

    public static bool IsWidthPointLeft(int index, int curvePointCount)
    {
        if (index < curvePointCount * 3)
        {
            return false;
        }

        return (index - (curvePointCount * 3)) % 2 == 0;
    }

    public static bool IsWidthPointRight(int index, int curvePointCount)
    {
        if (index < curvePointCount * 3)
        {
            return false;
        }

        return (index - (curvePointCount * 3)) % 2 == 1;
    }

    public static int GetCurveIndex(int index, int pointCount)
    {
        if (IsCenterPoint(index, pointCount))
        {
            return index;
        }

        if (IsControlPointIn(index, pointCount))
        {
            return (index - pointCount) / 2;
        }

        if (IsControlPointOut(index, pointCount))
        {
            return (index - pointCount - 1) / 2;
        }

        if (IsWidthPointLeft(index, pointCount) || IsWidthPointRight(index, pointCount))
        {
            return (index - (pointCount * 3)) / 2;
        }

        return -1;
    }

    public static int GetPointIndex(int curveIndex, bool isCenter, bool isPointIn, bool isPointOut, bool isWidthLeft, bool isWidthRight, int pointCount)
    {
        if (isCenter)
        {
            return curveIndex;
        }

        if (isPointIn)
        {
            return pointCount + (curveIndex * 2);
        }

        if (isPointOut)
        {
            return pointCount + 1 + (curveIndex * 2);
        }

        if (isWidthLeft)
        {
            return (pointCount * 3) + (curveIndex * 2);
        }

        if (isWidthRight)
        {
            return (pointCount * 3) + 1 + (curveIndex * 2);
        }

        return -1;
    }
}
