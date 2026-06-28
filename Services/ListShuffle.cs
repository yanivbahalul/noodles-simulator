using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace NoodlesSimulator.Services;

public static class ListShuffle
{
    public static void FisherYates<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
