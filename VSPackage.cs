//
// Copyright 2019 - Jeffrey "botman" Broome
//

using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System.IO;

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
	public sealed class VSPackage : AsyncPackage, IVsUpdateSolutionEvents2
	{
		/// <summary>
		/// VSPackage GUID string.
		/// </summary>
		public const string PackageGuidString = "2e208f6e-1d0e-436f-9b75-11b1219f32d5";

		private static AsyncPackage package;

		private static bool bNeedToAskForConfirmation = false;
		private static int LastCancelValue = 0;

		const VSSOLNBUILDUPDATEFLAGS REBUILD = VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD | VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_FORCE_UPDATE;

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

			IVsSolutionBuildManager buildManager = await GetServiceAsync(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager;
			if( buildManager != null)
			{
				uint buildManagerCookie;
				buildManager.AdviseUpdateSolutionEvents(this, out buildManagerCookie);
			}
		}

		// IVsUpdateSolutionEvents2 interface begin...
		public int UpdateSolution_Begin(ref int pfCancelUpdate)
		{
			bNeedToAskForConfirmation = true;
			LastCancelValue = 0;

			return VSConstants.S_OK;
		}

		public int UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand)
		{
			return VSConstants.S_OK;
		}

		public int UpdateSolution_StartUpdate(ref int pfCancelUpdate)
		{
			return VSConstants.S_OK;
		}

		public int UpdateSolution_Cancel()
		{
			return VSConstants.S_OK;
		}

		public int OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy)
		{
			return VSConstants.S_OK;
		}

		public int UpdateProjectCfg_Begin(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, ref int pfCancel)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (bNeedToAskForConfirmation)
			{
				bNeedToAskForConfirmation = false;

				string solutionDirectory = "";
				string solutionName = "";
				string solutionDirectory2 = "";

				IVsSolution solution = (IVsSolution) Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(IVsSolution));
				solution.GetSolutionInfo(out solutionDirectory, out solutionName, out solutionDirectory2);

				string solutionFileName = Path.GetFileName(solutionName).ToLower();

				if ((solutionFileName == "ue4.sln") || (solutionFileName == "ue5.sln"))
				{
					string BuildType = "";

					if (((VSSOLNBUILDUPDATEFLAGS)dwAction & REBUILD) == REBUILD)
					{
						BuildType = "Rebuild";
					}
					else if (((VSSOLNBUILDUPDATEFLAGS)dwAction & VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD) == VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD)
					{
						BuildType = "Build";
					}
					else if (((VSSOLNBUILDUPDATEFLAGS)dwAction & VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_CLEAN) == VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_CLEAN)
					{
						BuildType = "Clean";	
					}

					if (BuildType != "")
					{
						string message = String.Format("Do you want to {0} the Solution?", BuildType);
						string title = "WARNING!";

						int result = VsShellUtilities.ShowMessageBox(package, message, title, OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);

						if (result == (int)VSConstants.MessageBoxResult.IDNO)  // do we want to cancel the build?
						{
							LastCancelValue = 1;
						}
						else
						{
							LastCancelValue = 0;
						}
					}
				}
			}

			pfCancel = LastCancelValue;

			return VSConstants.S_OK;
		}

		public int UpdateProjectCfg_Done(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, int fSuccess, int fCancel)
		{
			return VSConstants.S_OK;
		}
		// IVsUpdateSolutionEvents2 interface end...

		#endregion
	}
}
