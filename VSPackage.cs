//
// Copyright 2019 - Jeffrey "botman" Broome
//

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace VerifyBuildSolution
{
	/// <summary>
	/// This is the class that implements the package exposed by this assembly.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The minimum requirement for a class to be considered a valid package for Visual Studio
	/// is to implement the IVsPackage interface and register itself with the shell.
	/// This package uses the helper classes defined inside the Managed Package Framework (MPF)
	/// to do it: it derives from the Package class that provides the implementation of the
	/// IVsPackage interface and uses the registration attributes defined in the framework to
	/// register itself and its components with the shell. These attributes tell the pkgdef creation
	/// utility what data to put into .pkgdef file.
	/// </para>
	/// <para>
	/// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
	/// </para>
	/// </remarks>
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
	[Guid(VSPackage.PackageGuidString)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]  // We want to auto load this extension so that it doesn't need to be activated by a command (it will always be available)
	public sealed class VSPackage : AsyncPackage
	{
		/// <summary>
		/// VSPackage GUID string.
		/// </summary>
		public const string PackageGuidString = "2e208f6e-1d0e-436f-9b75-11b1219f32d5";

		private static AsyncPackage package;
		private static DTE2 dte2;

		private static CommandEvents BuildCommandEvents;
		private static CommandEvents RebuildCommandEvents;
		private static CommandEvents CleanCommandEvents;

		#region Package Members

		/// <summary>
		/// Initialization of the package; this method is called right after the package is sited, so this is the place
		/// where you can put all the initialization code that rely on services provided by VisualStudio.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
		/// <param name="progress">A provider for progress updates.</param>
		/// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			// When initialized asynchronously, the current thread may be a background thread at this point.
			// Do any initialization that requires the UI thread after switching to the UI thread.
			await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			package = this;

			dte2 = Package.GetGlobalService(typeof(DTE)) as DTE2;

			Events2 evs = dte2.Events as Events2;
			Commands cmds = dte2.Commands;

			Command cmdobj = cmds.Item("Build.BuildSolution", 0);
			BuildCommandEvents = evs.get_CommandEvents(cmdobj.Guid, cmdobj.ID);
			BuildCommandEvents.BeforeExecute += new _dispCommandEvents_BeforeExecuteEventHandler(VerifyBuildSolution);

			cmdobj = cmds.Item("Build.RebuildSolution", 0);
			RebuildCommandEvents = evs.get_CommandEvents(cmdobj.Guid, cmdobj.ID);
			RebuildCommandEvents.BeforeExecute += new _dispCommandEvents_BeforeExecuteEventHandler(VerifyRebuildSolution);

			cmdobj = cmds.Item("Build.CleanSolution", 0);
			CleanCommandEvents = evs.get_CommandEvents(cmdobj.Guid, cmdobj.ID);
			CleanCommandEvents.BeforeExecute += new _dispCommandEvents_BeforeExecuteEventHandler(VerifyCleanSolution);
		}

		private static void VerifyBuildSolution(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			string solution_name = System.IO.Path.GetFileNameWithoutExtension(dte2.Solution.FullName).ToLower();

			if ((solution_name != "ue4") && (solution_name != "ue5"))
			{
				return;
			}

			string message = "Do you want to Build the Solution?";
			string title = "WARNING!";

			int result = VsShellUtilities.ShowMessageBox(package, message, title, OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);

			if (result == (int)VSConstants.MessageBoxResult.IDNO)  // do we want to cancel the build?
			{
				CancelDefault = true;
			}
			else
			{
				CancelDefault = false;
			}
		}

		private static void VerifyRebuildSolution(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			string solution_name = System.IO.Path.GetFileNameWithoutExtension(dte2.Solution.FullName).ToLower();

			if ((solution_name != "ue4") && (solution_name != "ue5"))
			{
				return;
			}

			string message = "Do you want to Rebuild the Solution?";
			string title = "WARNING!";

			int result = VsShellUtilities.ShowMessageBox(package, message, title, OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);

			if (result == (int)VSConstants.MessageBoxResult.IDNO)  // do we want to cancel the rebuild?
			{
				CancelDefault = true;
			}
			else
			{
				CancelDefault = false;
			}
		}

		private static void VerifyCleanSolution(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			string solution_name = System.IO.Path.GetFileNameWithoutExtension(dte2.Solution.FullName).ToLower();

			if ((solution_name != "ue4") && (solution_name != "ue5"))
			{
				return;
			}

			string message = "Do you want to Clean the Solution?";
			string title = "WARNING!";

			int result = VsShellUtilities.ShowMessageBox(package, message, title, OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);

			if (result == (int)VSConstants.MessageBoxResult.IDNO)  // do we want to cancel the clean?
			{
				CancelDefault = true;
			}
			else
			{
				CancelDefault = false;
			}
		}

		#endregion
	}
}
