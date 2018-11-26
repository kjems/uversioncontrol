// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;

namespace UVC
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    public class FilesAndFoldersComposedStringDatabase : BaseComposedSetDatabase<string>
    {
        const string regexSplitter = @"(\.)|(\/)|(\@)|(_)";
        static Regex regex = new Regex(regexSplitter, RegexOptions.Compiled);
        public override string[] Split(string composed)
        {
            return regex.Split(composed).Where(s => !string.IsNullOrEmpty(s)).ToArray();
        }

        static readonly StringBuilder sb = new StringBuilder();
        public override string Compose(List<int> indices)
        {
            sb.Clear();
            for (int i = 0, length = indices.Count; i < length; ++i)
            {
                sb.Append(Parts[indices[i]]);
            }
            return sb.ToString();
        }
    }
}