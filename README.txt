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

= Important Note =
When using UVersionControl without Unitys Team License it is highly recommended to 
use SVN v1.7 on both Windows and Mac. 

== Windows ==
On windows it is recommended to install Tortoise SVN 1.7+ with commandline enabled 
during the installation.

== Mac / OSX ==
It is recommended to install MacPorts. With MacPorts installed do 
sudo port install subversion. In Unity select the Settings item in UVC menu. 
Add /opt/local/bin/ to the environment path.

= Requirements =
 * Commandline svn client. A version 1.7 client is recommended.  
 * An already existing SVN checkout with credentials cached

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
 * Delete TeamFeatures.dll if your Unity does not have Team License.
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
  * TeamFeatures: is for Unity TeamLicense users only
  * RendererInspectors: is optional but gives direct access to VC commands
    on the inspector of a Renderer components.