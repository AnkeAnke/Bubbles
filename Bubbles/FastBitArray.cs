class FastBitArray
{
    public static int perInstanceSize;
    public static ulong[] array;
    
    static int curIndex;
    private int index;
    private int hashCode;
    public FastBitArray()
    {
        index = curIndex;
        curIndex += perInstanceSize;
    }

    public override int GetHashCode()
    {
        if (hashCode == 0)
            hashCode = RecalculateHashCode();
        return hashCode;
    }

    public override bool Equals(object? obj)
    {
        if (obj is FastBitArray fa)
        {
            for (int i = 0; i < perInstanceSize; i++)
            {
                if (array[i + index] != array[i + fa.index])
                    return false;
            }

            return true;
        }

        return false;
    }

    int RecalculateHashCode()
    {
        int hash = 17;
        for (int i = 0; i < perInstanceSize; i++)
        {
            hash = hash * 31 + (int)array[i + index];
        }
        return hash;
        
    }
    
    public bool this[int i]
    {
        //get => (array[index + i / sizeof(ulong)] >> (i % sizeof(ulong)) & 1) == 1;
        set
        {
            if (value)
                array[index + i / sizeof(ulong)] |= (ulong)1 << i % sizeof(ulong);
            else
                array[index + i / sizeof(ulong)] &= ~((ulong)1 << i % sizeof(ulong));
            hashCode = 0;
        }
    }
}