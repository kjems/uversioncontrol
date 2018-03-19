// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace VersionControl
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
                if (!composedToIndicies.TryGetValue(composed, out indices))
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
                    composedToIndicies.Add(composed, indices);
                }
            }
            return indices;
        }

        private readonly object constructorLockToken = new object();
        private readonly List<T> parts = new List<T>();
        private readonly Dictionary<T, int> partToIndex = new Dictionary<T, int>();
        private readonly Dictionary<T, List<int>> composedToIndicies = new Dictionary<T, List<int>>();
    }

    public class ComposedSet<T, TDB> where TDB : IComposedSetDatabase<T>, new()
    {
        // Const or Static
        protected static TDB database = new TDB();
        public static readonly ComposedSet<T, TDB> empty = new ComposedSet<T, TDB>(new List<int>());

        // Instance
        protected readonly List<int> indices;

        // Constructors
        protected ComposedSet() { }
        protected ComposedSet(List<int> indices)
        {
            this.indices = indices;
        }
        public ComposedSet(ComposedSet<T, TDB> cset)
        {
            indices = new List<int>(cset.indices);
        }
        public ComposedSet(T composed)
        {
            indices = database.Decompose(composed);
        } 

        public static implicit operator ComposedSet<T, TDB>(T composed)
        {
            return new ComposedSet<T, TDB>(composed);
        }

        public static ComposedSet<T, TDB> operator +(ComposedSet<T, TDB> a, ComposedSet<T, TDB> b)
        {
            return new ComposedSet<T, TDB>(a.indices.Concat(b.indices).ToList());
        }
        public static ComposedSet<T, TDB> operator +(ComposedSet<T, TDB> a, T b)
        {
            return new ComposedSet<T, TDB>(a.indices.Concat(new ComposedSet<T, TDB>(b).indices).ToList());
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
            if (cset == null) return true;
            if (cset.indices.Count == 0) return true;
            return false;
        }

        public T Compose()
        {
            return database.Compose(indices);
        }

        public List<int> GetIndicesCopy()
        {
            return new List<int>(indices);
        }
        public string GetIndiciesAsString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0, length = indices.Count; i < length; ++i)
            {
                sb.Append(indices[i]);
                if (i < length - 1) sb.Append(", ");
            }
            sb.Append("]");
            return sb.ToString();
        }
        
        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            var other = obj as ComposedSet<T, TDB>;
            if ((object)other == null) return false;
            if (other.indices.Count != indices.Count) return false;
            for (int i = 0, length = indices.Count; i < length; ++i)
            {
                if (other.indices[i] != indices[i]) return false;
            }
            return true;
        }
        public override int GetHashCode()
        {
            int hash = 13;
            for (int i = 0, length = indices.Count; i < length; ++i)
            {
                hash = (hash * 7) + indices[i];
            }
            return hash;
        }
        public bool EndsWith(ComposedSet<T, TDB> a)
        {
            int length = indices.Count;
            int alength = a.indices.Count;
            if (alength > length) return false;
            if (alength == 0) return false;
            for (int i = 0; i < alength; ++i)
            {
                if (a.indices[alength - i - 1] != indices[length - i - 1]) return false;
            }
            return true;
        }
        public bool StartsWith(ComposedSet<T, TDB> a)
        {
            int length = indices.Count;
            int alength = a.indices.Count;
            if (alength > length) return false;
            for (int i = 0; i < alength; ++i)
            {
                if (a.indices[i] != indices[i]) return false;
            }
            return true;
        }
        public ComposedSet<T, TDB> TrimEnd(ComposedSet<T, TDB> cset)
        {
            if (EndsWith(cset))
            {                
                var trimmed = new ComposedSet<T, TDB>(this);
                trimmed.indices.RemoveRange(trimmed.indices.Count - cset.indices.Count, cset.indices.Count);
                return trimmed;
            }
            return this;
        }
        /*public List<int> FindAll(ComposedString<T> cset)
        {
            int startIndex = 0;
            for (int i = 0, length = indices.Count; i < length; ++i)
            {
                for (int j = 0, flength = cset.indices.Count; j < flength; ++j)
                {

                }
            }
        }
        public void Replace(ComposedString<T> from, ComposedString<T> to)
        {
        }*/
    }
}