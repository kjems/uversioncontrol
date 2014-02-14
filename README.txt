UVersionControl README
======================

= Features =
 * An open source Version Control(VC) integration into Unity using either SVN 
   or Perforce as backend
 * GUI locks to avoid accidently modifing a resources without VC lock
 * Overview window showing current status on relevant files in relation to VC
 * Interactive icons on all assets in project- and hierarchy-view
 * SceneView gui to quickly get the lock on the current open scene
 * The concept of .meta files are abstracted away from the user


=== Subversion-Related Notes ===

= Setup Guide =
1) Make sure to have a command-line SVN client installed
2) Have an already existing SVN checkout in the root of a Unity Project folder
3) Import or install the Unity package containing UVC into the Unity Project folder
4) In the top menu find the UVC entry and select Overview Window and dock it somewhere
5) Press the red 'On/Off' toggle button and if all went well you should be ready to go

= Usage Guide =
a) Check the settings menu by clicking the settings button in the Overview Window
b) Use the icons in project and hierarchy view to do version control actions
c) Use the button in Sceneview to perform version control actions on current scene
d) In project view right-click a selection of objects and under UVC select action to perform
e) Use the Overview Window to perform actions and see the state of files under version control

== Windows ==
On windows it is recommended to install Tortoise SVN 1.7+ with commandline enabled 
during the installation.

== Mac / OSX ==
There are two recommended options for installing commandline SVN on OSX:

MacPorts
With MacPorts installed do sudo port install subversion. 
In Unity select the Settings item in UVC menu. Add /opt/local/bin/ to the environment path.

XCode
The newer versions of XCode comes with command line SVN 1.7+
Xcode > Preferences > Downloads > Command Line Tools > Install

=== Perforce-Related Notes ===

== Windows ==
Follow standard install procedures outlined on perforce's site for the visual client 
(P4V) and the CommandLine client (P4).

== Mac / OSX ==
Follow standard install procedures outlined on perforce's site for the visual client 
(P4V). For the CommandLine client, download P4 and put it in /usr/local/bin (also, 
make sure it is flagged as executable). In Unity select the Settings item in UVC 
menu. Add /usr/local/bin/ to the environment path.

= Requirements =
 * Commandline p4 client.  
 * An already existing P4 workspace (user/pass/server can all be set using the 
   connection tool) - this requirement will be going away in a future version
   of the tool


= Information =
 * See the Documentation folder for a structural overview of the code
 * See the MIT open source license in the TermsOfUse.txt
 * Download a set of pre-compiled assemblies from:
   https://bitbucket.org/Kjems/uversioncontrol/downloads

= How to compile and use =
 * Open the solution in ./VersionControlVS in MonoDevelop or Visual Studio
 * Build solution as release
 * Copy all assemblies from ./Assemblies/release/ to an Editor folder in Unity
 * Use following Unity guide on setting up version control:
http://unity3d.com/support/documentation/Manual/ExternalVersionControlSystemSupport

= Project Overview =
 == General C# ==
  * CommandLine: executes a commandline process
  * SVNBackend: is only project with SVN specific code. Uses CommandLine svn client.
  * P4Backend: is only project with Perforce specific code. Uses CommandLine p4 client.
  * Common: holds general version control concepts.

 == Unity Specific ==
  * UnityVersionControl: contains all Unity specific concerns
  * RendererInspectors: is optional but gives direct access to VC commands
    on the inspector of a Renderer components.