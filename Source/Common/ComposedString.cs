using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;

namespace VersionControl
{
    public class ComposedString
    {
        // Const or Static
        private const string regexSplit = @"(\.)|(\/)|(\@)|(_)";
        private static readonly List<string> stringList = new List<string>();
        private static readonly Dictionary<string, int> stringToIndex = new Dictionary<string, int>();
        private static readonly Dictionary<string, List<int>> stringToIndicies = new Dictionary<string, List<int>>();

        private static readonly object constructorLockToken = new object();

        public static List<string> GetStringListCopy()
        {
            return new List<string>(stringList);
        }

        // Instance
        private readonly List<int> indices;

        private ComposedString() { }
        private ComposedString(List<int> indices) { this.indices = indices; }

        public List<int> GetIndiciesCopy()
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

        public ComposedString(ComposedString cstr) { indices = new List<int>(cstr.indices); }
        public ComposedString(string str)
        {
            lock (constructorLockToken)
            {
                if (!stringToIndicies.TryGetValue(str, out indices))
                {
                    indices = new List<int>();
                    var parts = Regex.Split(str, regexSplit, RegexOptions.Compiled).Where(s => !string.IsNullOrEmpty(s));
                    foreach (var part in parts)
                    {
                        int index;
                        if (stringToIndex.TryGetValue(part, out index))
                        {
                            indices.Add(index);
                        }
                        else
                        {
                            stringList.Add(part);
                            int newIndex = stringList.Count - 1;
                            indices.Add(newIndex);
                            stringToIndex.Add(part, newIndex);
                            //D.Log("Tokens in ComposedString : " + stringList.Count + ", " + part + "\n" + stringList.Aggregate((a, b) => a + " | " + b));
                        }
                    }
                    stringToIndicies.Add(str, indices);
                }
            }
        }
        public static explicit operator string(ComposedString cstr)
        {
            return cstr.GetString();
        }
        public static implicit operator ComposedString(string str)
        {
            return new ComposedString(str);
        }
        public static ComposedString operator +(ComposedString a, ComposedString b)
        {
            return new ComposedString(a.indices.Concat(b.indices).ToList());
        }
        public static ComposedString operator +(ComposedString a, string b)
        {
            return new ComposedString(a.indices.Concat(new ComposedString(b).indices).ToList());
        }

        public static bool operator ==(ComposedString a, ComposedString b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (((object)a == null) || ((object)b == null)) return false;
            return a.Equals(b);
        }
        public static bool operator !=(ComposedString a, ComposedString b)
        {
            return !(a == b);
        }
        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            var other = obj as ComposedString;
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
        public string GetString()
        {
            StringBuilder sb = new StringBuilder(Length);
            for (int i = 0, length = indices.Count; i < length; ++i)
            {
                sb.Append(stringList[indices[i]]);
            }
            return sb.ToString();
        }
        public bool EndsWith(ComposedString a)
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
        public bool StartsWith(ComposedString a)
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
        public int Length
        {
            get
            {
                int count = 0;
                for (int i = 0, length = indices.Count; i < length; ++i)
                {
                    count += stringList[indices[i]].Length;
                }
                return count;
            }
        }
        public static bool IsNullOrEmpty(ComposedString cstr)
        {
            if (cstr == null) return true;
            if (cstr.indices.Count == 0) return true;
            return false;
        }
        public ComposedString TrimEnd(ComposedString cstr)
        {
            //D.Log("Before TrimEnd : '" + ToString() + "' "+ GetIndiciesAsString() +" Trimming '" + cstr.ToString() + "' " + cstr.GetIndiciesAsString());
            if (EndsWith(cstr))
            {
                //D.Log(ToString() + " EnsdWith " + cstr.ToString());
                var trimmed = new ComposedString(this);
                trimmed.indices.RemoveRange(trimmed.indices.Count - cstr.indices.Count, cstr.indices.Count);
                //D.Log("Trimmed After TrimEnd : '" + trimmed.ToString() + "' : " + trimmed.GetIndiciesAsString());
                //D.Log("Org After TrimEnd : '" + ToString() + "' : " + GetIndiciesAsString());
                return trimmed;
            }
            return this;
        }
        /*public List<int> FindAll(ComposedString cstr)
        {
            int startIndex = 0;
            for (int i = 0, length = indices.Count; i < length; ++i)
            {
                for (int j = 0, flength = cstr.indices.Count; j < flength; ++j)
                {

                }
            }
        }
        public void Replace(ComposedString from, ComposedString to)
        {
        }*/
    }
}