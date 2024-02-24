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
	[ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(UIContextGuids80.EmptySolution, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
	public sealed class VSPackage : AsyncPackage
	{
		/// <summary>
		/// VSPackage GUID string.
		/// </summary>
		public const string PackageGuidString = "2e208f6e-1d0e-436f-9b75-11b1219f32d5";

		private static AsyncPackage package;
		private static DTE dte;

		// the CommandEvents need to be 'static' for Visual Studio 2019
		private static CommandEvents BuildSolutionCommandEvent;
		private static CommandEvents RebuildSolutionCommandEvent;
		private static CommandEvents CleanSolutionCommandEvent;

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

#pragma warning disable VSSDK006 // Check services exist
			dte = await GetServiceAsync(typeof(SDTE)) as EnvDTE.DTE;
#pragma warning restore VSSDK006 // Check services exist

			if (dte != null)
			{
				BuildSolutionCommandEvent = dte.Events.CommandEvents["{5EFC7975-14BC-11CF-9B2B-00AA00573819}", 882]; // CLSID_StandardCommandSet97, cmdidBuildSln
				BuildSolutionCommandEvent.BeforeExecute += OnBeforeBuildSolution;

				RebuildSolutionCommandEvent = dte.Events.CommandEvents["{5EFC7975-14BC-11CF-9B2B-00AA00573819}", 883]; // CLSID_StandardCommandSet97, cmdidRebuildSln
				RebuildSolutionCommandEvent.BeforeExecute += OnBeforeRebuildSolution;

				CleanSolutionCommandEvent = dte.Events.CommandEvents["{5EFC7975-14BC-11CF-9B2B-00AA00573819}", 885]; // CLSID_StandardCommandSet97, cmdidCleanSln
				CleanSolutionCommandEvent.BeforeExecute += OnBeforeCleanSolution;
			}
		}

		private static void OnBeforeBuildSolution(string guid, int id, object customIn, object customOut, ref bool cancelDefault)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (!IsUnrealSolution())
			{
				return;
			}

			string message = "Do you want to Build the Solution?";
			string title = "WARNING!";

			int result = VsShellUtilities.ShowMessageBox(package, message, title, OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);

			if (result == (int)VSConstants.MessageBoxResult.IDNO)  // do we want to cancel the build?
			{
				cancelDefault = true;
			}
			else
			{
				cancelDefault = false;
			}
		}

		private static void OnBeforeRebuildSolution(string guid, int id, object customIn, object customOut, ref bool cancelDefault)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (!IsUnrealSolution())
			{
				return;
			}

			string message = "Do you want to Rebuild the Solution?";
			string title = "WARNING!";

			int result = VsShellUtilities.ShowMessageBox(package, message, title, OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);

			if (result == (int)VSConstants.MessageBoxResult.IDNO)  // do we want to cancel the rebuild?
			{
				cancelDefault = true;
			}
			else
			{
				cancelDefault = false;
			}
		}

		private static void OnBeforeCleanSolution(string guid, int id, object customIn, object customOut, ref bool cancelDefault)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (!IsUnrealSolution())
			{
				return;
			}

			string message = "Do you want to Clean the Solution?";
			string title = "WARNING!";

			int result = VsShellUtilities.ShowMessageBox(package, message, title, OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);

			if (result == (int)VSConstants.MessageBoxResult.IDNO)  // do we want to cancel the clean?
			{
				cancelDefault = true;
			}
			else
			{
				cancelDefault = false;
			}
		}

		private static bool IsUnrealSolution()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			IVsSolution2 solution = Package.GetGlobalService(typeof(SVsSolution)) as IVsSolution2;
			IVsHierarchy solutionHierarchy = (IVsHierarchy)solution;
			IVsProject solutionProject = null;

			GetHierarchyInSolution(solutionHierarchy, VSConstants.VSITEMID_ROOT, ref solutionProject, "Engine", "", "",
													out IVsHierarchy EngineHierarchy, out IVsProject EngineProject, out uint EngineItemId, out string Unused1);
			if ((EngineHierarchy != null) && (EngineProject != null))
			{
				GetHierarchyInSolution(EngineHierarchy, VSConstants.VSITEMID_ROOT, ref EngineProject, "UE4", "", "",
														out IVsHierarchy UE4Hierarchy, out IVsProject UE4Project, out uint UE4ItemId, out string Unused2);
				if ((UE4Hierarchy != null) && (UE4Project != null))
				{
					return true;
				}

				GetHierarchyInSolution(EngineHierarchy, VSConstants.VSITEMID_ROOT, ref EngineProject, "UE5", "", "",
														out IVsHierarchy UE5Hierarchy, out IVsProject UE5Project, out uint UE5ItemId, out string Unused3);
				if ((UE5Hierarchy != null) && (UE5Project != null))
				{
					return true;
				}
			}

			return false;
		}

		// searches the solution for a specific project name, folder name, or file name
		private static void GetHierarchyInSolution(IVsHierarchy hierarchy, uint itemId, ref IVsProject Project, string SearchProject, string SearchFolder, string SearchFileName, out IVsHierarchy OutHierarchy, out IVsProject OutProject, out uint OutItemId, out string OutFilename)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			char[] InvalidChars = Path.GetInvalidPathChars();  // get characters not allowed in file paths

			OutHierarchy = null;
			OutProject = null;
			OutItemId = VSConstants.VSITEMID_ROOT;
			OutFilename = "";

			try
			{
				// NOTE: If itemId == VSConstants.VSITEMID_ROOT then this hierarchy is a solution, project, or folder in the Solution Explorer

				if (hierarchy == null)
				{
					return;
				}

				object ChildObject = null;

				// Get the first visible child node
				if (hierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_FirstVisibleChild, out ChildObject) == VSConstants.S_OK)
				{
					while (ChildObject != null)
					{
						if ((ChildObject is int) && ((uint)(int)ChildObject == VSConstants.VSITEMID_NIL))
						{
							break;
						}

						uint visibleChildNodeId = Convert.ToUInt32(ChildObject);

						object nameObject = null;

						if ((hierarchy.GetProperty(visibleChildNodeId, (int)__VSHPROPID.VSHPROPID_Name, out nameObject) == VSConstants.S_OK) && (nameObject != null))
						{
							if ((string)nameObject == SearchProject)
							{
								Guid nestedHierarchyGuid = typeof(IVsHierarchy).GUID;
								IntPtr nestedHiearchyValue = IntPtr.Zero;
								uint nestedItemIdValue = 0;

								// see if the child node has a nested hierarchy (i.e. is it a project?, is it a folder?, etc.)...
								if ((hierarchy.GetNestedHierarchy(visibleChildNodeId, ref nestedHierarchyGuid, out nestedHiearchyValue, out nestedItemIdValue) == VSConstants.S_OK) &&
									(nestedHiearchyValue != IntPtr.Zero && nestedItemIdValue == VSConstants.VSITEMID_ROOT))
								{
									// Get the new hierarchy
									IVsHierarchy nestedHierarchy = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(nestedHiearchyValue) as IVsHierarchy;
									System.Runtime.InteropServices.Marshal.Release(nestedHiearchyValue);

									if (nestedHierarchy != null)
									{
										OutHierarchy = nestedHierarchy;
										OutProject = (IVsProject)nestedHierarchy;

										return;
									}
								}
							}
							else
							{
								object NodeChildObject = null;

								// see if this regular node has children...
								if ((string)nameObject == SearchFolder)
								{
									if (hierarchy.GetProperty(visibleChildNodeId, (int)__VSHPROPID.VSHPROPID_FirstVisibleChild, out NodeChildObject) == VSConstants.S_OK)
									{
										if (NodeChildObject != null)
										{
											if ((NodeChildObject is int) && ((uint)(int)NodeChildObject != VSConstants.VSITEMID_NIL))
											{
												OutHierarchy = hierarchy;
												OutProject = Project;
												OutItemId = visibleChildNodeId;
											}
										}
									}
								}

								if ((string)nameObject == SearchFileName)
								{
									try
									{
										string projectFilename = "";

										if (Project.GetMkDocument(visibleChildNodeId, out projectFilename) == VSConstants.S_OK)
										{
											if ((projectFilename != null) && (projectFilename.Length > 0) &&
												(!projectFilename.EndsWith("\\")) &&  // some invalid "filenames" will end with '\\'
												(projectFilename.IndexOfAny(InvalidChars) == -1) &&
												(projectFilename.IndexOf(":", StringComparison.OrdinalIgnoreCase) == 1))  // make sure filename is of the form: drive letter followed by colon
											{
												OutFilename = projectFilename;
											}
										}
									}
									catch (Exception)
									{
									}
								}
							}
						}

						ChildObject = null;

						// Get the next visible sibling node
						if (hierarchy.GetProperty(visibleChildNodeId, (int)__VSHPROPID.VSHPROPID_NextVisibleSibling, out ChildObject) != VSConstants.S_OK)
						{
							break;
						}
					}
				}
			}
			catch (Exception)
			{
			}
		}

		#endregion
	}
}
