UVersionControl README
======================

## Features
 * An open source Version Control(VC) integration into Unity using SVN as backend
 * GUI locks to avoid accidently modifing a resources without VC lock
 * Overview window showing current status on relevant files in relation to VC
 * Interactive icons on all assets in project- and hierarchy-view
 * SceneView gui to quickly get the lock on the current active scene
 * The concept of .meta files are abstracted away from the user

## Setup Guide
 1) Make sure to have a command-line SVN client installed
 2) Have an already existing SVN checkout in the root of a Unity Project folder
 3) Make sure to turn on visible meta files 
    (Edit -> Project Settings -> Editor -> Version Control -> Mode)
 4) make sure it is using .NET 4.x 
    (Edit -> Project Settings -> Player -> Other Settings -> Configuration -> Scripting runtime version)
 5) Clone the uversioncontrol Git repo into a folder in the '[YourProject]/Packages' directory
 6) In the top menu find the UVC entry and select Overview Window and dock it somewhere
 7) Press the red 'On/Off' toggle button and if all went well you should be ready to go

## Usage Guide
 * Check the settings menu by clicking the settings button in the Overview Window
 * Use the icons in project and hierarchy view to do version control actions
 * Use the button in Sceneview to perform version control actions on current scene
 * In project view right-click a selection of objects and under UVC select action to perform
 * Use the Overview Window to perform actions and see the state of files under version control

## Subversion-Related Notes

#### Windows
On windows it is recommended to install Tortoise SVN 1.7+ with commandline enabled 
during the installation.

#### Mac / OSX
There are two recommended options for installing commandline SVN on OSX:

##### MacPorts
With MacPorts installed do sudo port install subversion. 
In Unity select the Settings item in UVC menu. Add /opt/local/bin/ to the environment path.

##### XCode
The newer versions of XCode comes with command line SVN 1.7+
Xcode > Preferences > Downloads > Command Line Tools > Install

## Information
 * See the Documentation folder for a structural overview of the code
 * See the MIT open source license in the TermsOfUse.txt
 
## How to use
 * Add to Unity project Package folder or Assets folder

## Project Overview
### General C#
  * CommandLine: executes a commandline process
  * SVNBackend: is only project with SVN specific code. Uses CommandLine svn client.
  * Common: holds general version control concepts.

### Unity Specific
  * UnityVersionControl: contains all Unity specific concerns
  * RendererInspectors: is optional but gives direct access to VC commands
    on the inspector of a Renderer components.
