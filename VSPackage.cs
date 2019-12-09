//
// Copyright 2019 - Jeffrey "botman" Broome
//

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;

using EnvDTE;
using EnvDTE80;
using System.Windows.Forms;

// NOTE: The instructions for converting a VS2015 VSIX Package to AsyncPackage can be found here:
// https://docs.microsoft.com/en-us/visualstudio/extensibility/how-to-use-asyncpackage-to-load-vspackages-in-the-background?view=vs-2019

// NOTE: Instructions on creating extensions for multiple Visual Studio versions can be found here:
// https://docs.microsoft.com/en-us/archive/msdn-magazine/2017/august/visual-studio-creating-extensions-for-multiple-visual-studio-versions

// NOTE: Instructions for building VSIX v3 format manifests (to make the VS2015 extension compatible with VS2017 and VS2019 can be found here:
// https://github.com/MicrosoftDocs/visualstudio-docs/blob/master/docs/extensibility/faq-2017.md#can-i-build-a-vsix-v3-with-visual-studio-2015

// NOTE: Also see these instructions on updating NuGet package to support the VSIXv3 schema:
// https://docs.microsoft.com/en-us/visualstudio/extensibility/how-to-roundtrip-vsixs?view=vs-2019
// (I had to create a folder named 'v3' under VerifyBuildSolution\packages\Microsoft.VSSDK.BuildTools.16.3.2099\tools\vssdk\schemas and move the *.xsd files there)

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
	[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]  // We want to auto load this extension so that it doesn't need to be activated by a command (it will always be available)
	public sealed class VSPackage : AsyncPackage
	{
		/// <summary>
		/// VSPackage GUID string.
		/// </summary>
		public const string PackageGuidString = "2e208f6e-1d0e-436f-9b75-11b1219f32d5";

		/// <summary>
		/// Initializes a new instance of the <see cref="VSPackage"/> class.
		/// </summary>
		public VSPackage()
		{
			// Inside this method you can place any initialization code that does not require
			// any Visual Studio service because at this point the package object is created but
			// not sited yet inside Visual Studio environment. The place to do all the other
			// initialization is the Initialize method.
		}

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
			VerifyBuildSolutionTask.Initialize(this);
		}

		#endregion
	}

	internal sealed class VerifyBuildSolutionTask
	{
		/// <summary>
		/// VS Package that provides this command, not null.
		/// </summary>
		private readonly AsyncPackage package;
		private static DTE2 dte2;

		private static CommandEvents BuildCommandEvents;
		private static CommandEvents RebuildCommandEvents;
		private static CommandEvents CleanCommandEvents;

		/// <summary>
		/// Initializes a new instance of the <see cref="VerifyBuildSolutionTask"/> class.
		/// Adds our command handlers for menu (commands must exist in the command table file)
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		private VerifyBuildSolutionTask(AsyncPackage package)
		{
			this.package = package;

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

		/// <summary>
		/// Gets the instance of the command.
		/// </summary>
		public static VerifyBuildSolutionTask Instance
		{
			get;
			private set;
		}

		/// <summary>
		/// Initializes the singleton instance of the command.
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		public static void Initialize(AsyncPackage package)
		{
			// Verify the current thread is the UI thread
			ThreadHelper.ThrowIfNotOnUIThread();

			Instance = new VerifyBuildSolutionTask(package);
		}

		private static void VerifyBuildSolution(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
		{
			string solution_name = System.IO.Path.GetFileNameWithoutExtension(dte2.Solution.FullName).ToLower();

			if (solution_name != "ue4")
			{
				return;
			}

			DialogResult result = MessageBox.Show(null, "Do you want to Build the Solution", "WARNING!", MessageBoxButtons.YesNo,
				MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

			if (result == DialogResult.No)
			{
				CancelDefault = true;
			}
		}

		private static void VerifyRebuildSolution(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
		{
			string solution_name = System.IO.Path.GetFileNameWithoutExtension(dte2.Solution.FullName).ToLower();

			if (solution_name != "ue4")
			{
				return;
			}

			DialogResult result = MessageBox.Show(null, "Do you want to Rebuild the Solution", "WARNING!", MessageBoxButtons.YesNo,
				MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

			if (result == DialogResult.No)
			{
				CancelDefault = true;
			}
		}

		private static void VerifyCleanSolution(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
		{
			string solution_name = System.IO.Path.GetFileNameWithoutExtension(dte2.Solution.FullName).ToLower();

			if (solution_name != "ue4")
			{
				return;
			}

			DialogResult result = MessageBox.Show(null, "Do you want to Clean the Solution", "WARNING!", MessageBoxButtons.YesNo,
				MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

			if (result == DialogResult.No)
			{
				CancelDefault = true;
			}
		}
	}
}
