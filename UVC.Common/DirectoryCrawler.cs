// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace UVC
{
   // this class builds a list of directories under the desired parent directory
   public class DirectoryCrawler
   {
      public class DirectoryData
      {
         public string fullName;
         public DirectoryData parent = null;
         public List<DirectoryData> directories = new List<DirectoryData>();
         
         public DirectoryData(DirectoryInfo info, DirectoryData _parent = null, bool storeRelativePath = false, string rootPath = "")
         {
            fullName = info.FullName;
            if ( storeRelativePath ) {
                fullName = fullName.Replace( rootPath + Path.DirectorySeparatorChar.ToString(), "" );
            }
            parent = _parent;
            if ( parent != null ) {
               parent.directories.Add( this );
            }
         }
      }
      
      private List<DirectoryData> pathData = new List<DirectoryData>();
      public List<DirectoryData> Directories { get { return pathData; } }
      
      private string searchPattern = "*";
      public string SearchPattern {
         get { return searchPattern; }
         set {
            if ( value != searchPattern ) {
               searchPattern = value;
               Refresh();
            }
         }
      }
      
      private string directory = "";
      public string Directory { 
         get { return directory; } 
         set { 
            if ( value != directory ) {
               directory = value; 
               Refresh();
            }
         } 
      }
      
      private int searchDepth = -1;
      public int SearchDepth {
         get { return searchDepth; }
         set {
            if ( value != searchDepth ) {
               searchDepth = value;
               Refresh();
            }
         }
      }
      
      private List<string> ignoreStrings = null;
      public List<string> IgnoreStrings {
          get { return ignoreStrings; }
          set { 
              if ( value != null ) {
                  ignoreStrings = new List<string>();
                  foreach ( string pattern in value ) { ignoreStrings.Add(WildcardToRegexPattern(pattern)); }
                  Refresh();
              }
          }
      }
      
      private bool storeRelativePaths = false;
      public bool StoreRelativePaths {
         get { return storeRelativePaths; }
         set {
            if ( value != storeRelativePaths ) {
               storeRelativePaths = value;
               Refresh();
            }
         }
      }
      
      public DirectoryCrawler()
      {
      }

      public DirectoryCrawler(string dir, string pattern = "*", int depth = -1)
      {
         searchPattern = pattern;
         searchDepth = depth;
         Directory = dir;
      }
      
      public void Refresh()
      {
         pathData.Clear();
         if ( !string.IsNullOrEmpty(directory) ) {
             GetDirectoryData(null, directory);
         }
      }
        
      public static string WildcardToRegexPattern(string wildcard, bool matchWholeWord = true)
      {
         if (String.IsNullOrEmpty(wildcard))
         {
            wildcard = "*";
         }

         // Replace ; with |
         string[] strings = wildcard.Split(';');
         for (int i = 0; i < strings.Length; ++i)
         {
            // Remove extra spaces and escape any special characters and convert wildcards into regex tokens
            strings[i] = Regex.Escape(strings[i].Trim()).Replace("\\*", ".*").Replace("\\?", ".");
         }
         wildcard = string.Join("|", strings);

         // If the match whole word flag is set then add the required characters to the ends of the string
         if (matchWholeWord) wildcard = "^" + wildcard + "$";

         return wildcard;
      }
        
      private void GetDirectoryData(DirectoryData parentData, string directory, string tabString = "", int currentDepth = 0)
      {
         if ( searchDepth != -1 && currentDepth <= searchDepth ) {
             System.IO.DirectoryInfo dInfo = new System.IO.DirectoryInfo(directory);
             if ( dInfo.Exists ) {
                DirectoryData dd = new DirectoryData(dInfo, parentData, storeRelativePaths, Directory);
                pathData.Add(dd);
                //D.Log(tabString + "DIR: " + directory);
                DirectoryInfo[] dirs = dInfo.GetDirectories(searchPattern);
                    
                // filter out any dirs that should be ignored
                if ( ignoreStrings != null ) {
                    List<DirectoryInfo> newDirs = new List<DirectoryInfo>();
                    foreach( DirectoryInfo di in dirs ) {
                        string path = di.FullName.Replace(directory + Path.DirectorySeparatorChar.ToString(), "");
                        bool ignore = false;
                        foreach( string pattern in ignoreStrings ) {
                            if ( Regex.IsMatch( path, pattern ) ) {
                                ignore = true;
                                break;
                            }
                        }
                        if ( !ignore ) newDirs.Add(di);
                    }
                    dirs = newDirs.ToArray();
                }
                
                // recurse through directories
                foreach( DirectoryInfo di in dirs ) {
                   GetDirectoryData( dd, di.FullName, tabString + "\t", currentDepth + 1 );
                }
             }
             else {
                throw new Exception("In GetDirectoryData, but the directory passed (" + directory + ") does not exist!");
             }
          }
      }
   }
}

