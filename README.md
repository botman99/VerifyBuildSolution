# Verify Build Solution

Verify Build Solution is a Visual Studio extension for Unreal Engine 4 and Unreal Engine 5 that will ask for confirmation before Building, Rebuilding, or Cleaning the Solution.  It does this by inserting itself into the Visual Studio User Interface and will display a dialog box asking for confirmation before building, rebuilding or cleaning the solution (but only if the solution is named UE4.sln or UE5.sln).  This Visual Studio extension supports Visual Studio 2017, 2019 and 2022.

[![screenshot](https://github.com/botman99/VerifyBuildSolution/raw/master/RebuildWarning.png)](https://github.com/botman99/VerifyBuildSolution/raw/master/RebuildWarning.png)

NOTE: Since the extension is using asynchronous loading, it is not immediately available and may take 5 or 10 seconds to enable itself after loading the solution.  If you build, rebuild or clean the solution before the extension has fully loaded, you will not get the confirmation dialog.

See the [Releases](https://github.com/botman99/VerifyBuildSolution/releases) page to download the latest release.

* Author: Jeffrey "botman" Broome
* License: [MIT](http://opensource.org/licenses/mit-license.php)
