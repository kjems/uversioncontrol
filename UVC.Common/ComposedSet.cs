// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using Unity.Profiling;

namespace UVC
{
    public interface IComposedSetDatabase<T>
    {
        List<int> Decompose(T composed);
        T Compose(List<int> decomposed);
    }

    public abstract class BaseComposedSetDatabase<T> : IComposedSetDatabase<T>
    {
        public abstract T[] Split(T composed);
        public abstract T Compose(List<int> decomposed);

        public List<T> Parts { get { return parts; } }

        public List<int> Decompose(T composed)
        {
            List<int> indices;
            lock (constructorLockToken)
            {
                if (!composedToIndices.TryGetValue(composed, out indices))
                {
                    indices = new List<int>();
                    foreach (var part in Split(composed))
                    {
                        int index;
                        if (partToIndex.TryGetValue(part, out index))
                        {
                            indices.Add(index);
                        }
                        else
                        {
                            parts.Add(part);
                            int newIndex = parts.Count - 1;
                            indices.Add(newIndex);
                            partToIndex.Add(part, newIndex);
                        }
                    }
                    composedToIndices.Add(composed, indices);
                }
            }
            return indices;
        }

        private readonly object constructorLockToken = new object();
        private readonly List<T> parts = new List<T>();
        private readonly Dictionary<T, int> partToIndex = new Dictionary<T, int>();
        private readonly Dictionary<T, List<int>> composedToIndices = new Dictionary<T, List<int>>();
    }

    public class ComposedSet<T, TDB> where TDB : IComposedSetDatabase<T>, new()
    {
        #region Profiler Markers
        private static readonly ProfilerMarker composeMarker   = new ProfilerMarker("Compose");
        private static readonly ProfilerMarker decomposeMarker = new ProfilerMarker("Decompose");
        private static readonly ProfilerMarker equalsMarker    = new ProfilerMarker("Equals");
        private static readonly ProfilerMarker hashcodeMarker  = new ProfilerMarker("HashCode");
        #endregion
        
        // Const or Static
        protected static TDB database = new TDB();
        public static readonly ComposedSet<T, TDB> empty = new ComposedSet<T, TDB>(new List<int>());

        // Instance
        protected readonly List<int> indices;
        protected readonly int hashCode;

        // Constructors
        protected ComposedSet() { }
        protected ComposedSet(List<int> indices)
        {
            this.indices = indices;
            hashCode = CalculateHashCode(indices);
        }
        public ComposedSet(ComposedSet<T, TDB> cset)
        {
            indices = new List<int>(cset.indices);
            hashCode = CalculateHashCode(indices);
        }
        public ComposedSet(T composed)
        {
            using (decomposeMarker.Auto())
            {
                indices = database.Decompose(composed);
            }
            hashCode = CalculateHashCode(indices);
        }

        public static implicit operator ComposedSet<T, TDB>(T composed)
        {
            return new ComposedSet<T, TDB>(composed);
        }
        public static ComposedSet<T, TDB> operator +(ComposedSet<T, TDB> a, ComposedSet<T, TDB> b)
        {
            var newIndices = new List<int>(a.indices);
            newIndices.AddRange(b.indices);
            return new ComposedSet<T, TDB>(newIndices);
        }
        public static ComposedSet<T, TDB> operator +(ComposedSet<T, TDB> a, T b)
        {
            var newIndices = new List<int>(a.indices);
            var bComposedSet = new ComposedSet<T, TDB>(b);
            newIndices.AddRange(bComposedSet.indices);
            return new ComposedSet<T, TDB>(newIndices);
        }
        public static bool operator ==(ComposedSet<T, TDB> a, ComposedSet<T, TDB> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (((object)a == null) || ((object)b == null)) return false;
            return a.Equals(b);
        }
        public static bool operator !=(ComposedSet<T, TDB> a, ComposedSet<T, TDB> b)
        {
            return !(a == b);
        }
        public static bool IsNullOrEmpty(ComposedSet<T, TDB> cset)
        {
            return cset == null || cset.indices.Count == 0;
        }
        private static int CalculateHashCode(List<int> indices)
        {
            using (hashcodeMarker.Auto())
            {
                int hash = 13;
                for (int i = 0, length = indices.Count; i < length; ++i)
                    hash = (hash * 7) + indices[i];
                return hash;
            }
        }
        public T Compose()
        {
            using (composeMarker.Auto())
            {
                return database.Compose(indices);
            }
        }
        public List<int> GetIndicesCopy()
        {
            return new List<int>(indices);
        }
        public override bool Equals(object obj)
        {
            using (equalsMarker.Auto())
            {
                if (obj == null) return false;
                var other = obj as ComposedSet<T, TDB>;
                if ((object) other == null) return false;
                if (other.hashCode != hashCode) return false;

                for (int i = 0, length = indices.Count; i < length; ++i)
                    if (other.indices[i] != indices[i])
                        return false;

                return true;
            }
        }

        public override string ToString()
        {
            return Compose().ToString();
        }

        public override int GetHashCode()
        {
            return hashCode;
        }
        public bool EndsWith(ComposedSet<T, TDB> a)
        {
            int length = indices.Count;
            var aindices = a.indices;
            int alength = aindices.Count;

            if (alength > length) return false;
            if (alength == 0) return false;
            
            for (int i = 1, count = alength + 1; i < count ; ++i)
                if (aindices[alength - i] != indices[length - i]) return false;
            
            return true;
        }
        public bool StartsWith(ComposedSet<T, TDB> a)
        {
            int length = indices.Count;
            int alength = a.indices.Count;

            if (alength > length) return false;
            if (alength == 0) return false;
            
            for (int i = 0; i < alength; ++i)
                if (a.indices[i] != indices[i]) return false;

            return true;
        }
        public ComposedSet<T, TDB> TrimEnd(ComposedSet<T, TDB> cset)
        {
            return EndsWith(cset) ? GetSubset(0, indices.Count - cset.indices.Count) : this;
        }

        public bool Contains(ComposedSet<T, TDB> a)
        {
            return FindFirstIndex(a) != -1;
        }

        public int FindFirstIndex(ComposedSet<T, TDB> a)
        {
            int length = indices.Count;
            var aindices = a.indices;
            int alength = aindices.Count;
            if (length == 0) return -1;
            if (alength > length) return -1;
            if (alength == 0) return -1;
            
            for (int i = 0; i < length - (alength - 1); i++)
            {
                if (indices[i] == aindices[0]) // First index match, now test the rest
                {
                    for (int j = 0; j < alength; j++)
                    {
                        if (aindices[j] != indices[i + j])
                            break; // Mis-match continue searching for a matching first index
                        if (j == alength - 1)
                            return i; // Rest was a match, return index of matching start index
                    }
                }
            }
            return -1;
        }
        
        public int FindLastIndex(ComposedSet<T, TDB> a)
        {
            int length = indices.Count;
            var aindices = a.indices;
            int alength = aindices.Count;
            if (length == 0) return -1;
            if (alength > length) return -1;
            if (alength == 0) return -1;
            
            for (int i = length - 1; i >= 0; i--) // look from end to start
            {
                if (indices[i] == aindices[0] && i + alength <= length) // First index match, now test the rest
                {
                    for (int j = 0; j < alength; j++)
                    {
                        if (aindices[j] != indices[i + j])
                            break; // Mis-match continue searching for a matching first index
                        if (j == alength - 1)
                            return i; // Rest was a match, return index of matching start index
                    }
                }
            }
            return -1;
        }

        public ComposedSet<T, TDB> GetSubset(int startIndex, int length)
        {
            if (startIndex == 0 && length == indices.Count)
            {
                return this;
            }
            return new ComposedSet<T, TDB>(indices.GetRange(startIndex, length));
        }
    }
}