using Godot;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

public class BlogPost
{
	public string Title { get; set; }
	public string Date { get; set; }
	public string Excerpt { get; set; }
	public string Content { get; set; }
}

public partial class LauncherMain : Control
{
	private CheckButton _modeToggle;
	private bool _isServerMode = false;
	private string _clientSavedPath = @"C:\Games\EQMUD";
	private string _serverSavedPath = @"C:\Servers\EQMUD_Server";
	
	private OptionButton _clientActionCombo;
	private LineEdit _clientPathEdit;
	private Button _clientBrowseBtn;
	private Button _clientActionBtn;
	private EqStyleDownloadBar _clientProgress;
	private Label _clientStatus;
	private Button _discordBtn;
	
	private Panel _passwordDialog;
	private LineEdit _passwordEdit;
	private Button _passwordSubmitBtn;
	private Button _passwordCancelBtn;
	
	private FileDialog _clientFolderDialog;

	// Blog UI
	private Control _leftBlog;
	private Label _leftBlogTitle;
	private Control _rightBlog;
	private Label _rightBlogTitle;
	private Control _centerBlog;
	private Label _centerBlogTitle;
	private Label _centerBlogDate;
	private Label _centerBlogExcerpt;
	private Button _centerClickBtn;
	
	private Button _prevBtn;
	private Button _nextBtn;
	
	private Panel _fullBlogDialog;
	private Label _fullBlogTitle;
	private Label _fullBlogDate;
	private RichTextLabel _fullBlogContent;
	private Button _fullBlogCloseBtn;
	
	private List<BlogPost> _blogs = new List<BlogPost>();
	private int _currentBlogIndex = 0;

	// LanternExtractor is a runtime dependency (used by the client to extract EQ assets on demand).
	// We download it during Install/Update if missing so it doesn't need to be committed into exports.
	private const string LanternExtractorTag = "0.1.7";
	private const string LanternExtractorExeName = "LanternExtractor.exe";
	private string LanternExtractorZipUrl =>
		$"https://github.com/LanternEQ/LanternExtractor/releases/download/{LanternExtractorTag}/" +
		$"LanternExtractor-{LanternExtractorTag}.win-x64.zip";

	// EQ.gd client is net8.0 (see eqmud/EQMUD.csproj). LanternExtractor 0.1.7 targets .NET 6.
	// Official x64 runtime installers (quiet install may prompt UAC once).
	private const int ClientNetMajor = 8;
	private const int LanternNetMajor = 6;
	// Windows Desktop Runtime includes what many desktop-style hosts expect; it is a safe default for EQ.gd.
	private const string DotNet8DesktopInstallerUrl = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe";
	private const string DotNet6RuntimeInstallerUrl = "https://aka.ms/dotnet/6.0/dotnet-runtime-win-x64.exe";

	// Client: always pulls the latest GitHub release. Cutting a new release on
	// KaelKodes/Everquest-Godot-Client is all it takes for the launcher to ship
	// an update — no hardcoded tag or asset filename to keep in sync.
	private const string ClientGithubOwner = "KaelKodes";
	private const string ClientGithubRepo = "Everquest-Godot-Client";
	private string ClientGithubReleaseApiUrl =>
		$"https://api.github.com/repos/{ClientGithubOwner}/{ClientGithubRepo}/releases/latest";

	// Server: pulls the latest GitHub release.
	private const string ServerGithubOwner = "KaelKodes";
	private const string ServerGithubRepo = "Everquest-Godot-Server";
	private string ServerGithubReleaseApiUrl =>
		$"https://api.github.com/repos/{ServerGithubOwner}/{ServerGithubRepo}/releases/latest";

	private sealed class ClientInstallState
	{
		public string Tag { get; set; }
		public string Asset { get; set; }
		public string Sha256 { get; set; }
	}

	private sealed class ClientReleaseZipAsset
	{
		public string ReleaseTag { get; set; }
		public string Name { get; set; }
		public string BrowserDownloadUrl { get; set; }
		public long Size { get; set; }
		public string Sha256Hex { get; set; }
	}

	public override void _Ready()
	{
		// Client UI
		_clientActionCombo = GetNode<OptionButton>("ClientUI/ActionCombo");
		_clientPathEdit = GetNode<LineEdit>("ClientUI/PathEdit");
		_clientBrowseBtn = GetNode<Button>("ClientUI/BrowseBtn");
		_clientActionBtn = GetNode<Button>("ClientUI/ActionBtn");
		_clientProgress = GetNode<EqStyleDownloadBar>("ClientUI/ProgressBar");
		_clientStatus = GetNode<Label>("ClientUI/StatusLabel");
		_modeToggle = GetNode<CheckButton>("ClientUI/ModeToggle");
		_discordBtn = GetNode<Button>("DiscordBtn");
		
		// Password Dialog
		_passwordDialog = GetNode<Panel>("PasswordDialog");
		_passwordEdit = GetNode<LineEdit>("PasswordDialog/PasswordEdit");
		_passwordSubmitBtn = GetNode<Button>("PasswordDialog/SubmitBtn");
		_passwordCancelBtn = GetNode<Button>("PasswordDialog/CancelBtn");
		
		// File Dialogs
		_clientFolderDialog = GetNode<FileDialog>("ClientFolderDialog");

		// Blog UI Setup
		_leftBlog = GetNode<Control>("BlogContainer/LeftBlog");
		_leftBlogTitle = GetNode<Label>("BlogContainer/LeftBlog/Title");
		_rightBlog = GetNode<Control>("BlogContainer/RightBlog");
		_rightBlogTitle = GetNode<Label>("BlogContainer/RightBlog/Title");
		
		_centerBlog = GetNode<Control>("BlogContainer/CenterBlog");
		_centerBlogTitle = GetNode<Label>("BlogContainer/CenterBlog/Title");
		_centerBlogDate = GetNode<Label>("BlogContainer/CenterBlog/Date");
		_centerBlogExcerpt = GetNode<Label>("BlogContainer/CenterBlog/Excerpt");
		_centerClickBtn = GetNode<Button>("BlogContainer/CenterBlog/CenterClickBtn");
		
		_prevBtn = GetNode<Button>("BlogContainer/PrevBtn");
		_nextBtn = GetNode<Button>("BlogContainer/NextBtn");
		
		_fullBlogDialog = GetNode<Panel>("FullBlogDialog");
		_fullBlogTitle = GetNode<Label>("FullBlogDialog/Title");
		_fullBlogDate = GetNode<Label>("FullBlogDialog/Date");
		_fullBlogContent = GetNode<RichTextLabel>("FullBlogDialog/Content");
		_fullBlogCloseBtn = GetNode<Button>("FullBlogDialog/CloseBtn");

		// Setup Combo
		_clientActionCombo.ItemSelected += OnActionSelected;
		
		// Connections
		_modeToggle.Toggled += OnModeToggled;
		_clientBrowseBtn.Pressed += () => _clientFolderDialog.PopupCentered(new Vector2I(600, 400));
		_clientFolderDialog.DirSelected += (dir) => _clientPathEdit.Text = dir;

		_clientActionBtn.Pressed += OnClientActionExecute;
		
		_passwordSubmitBtn.Pressed += OnPasswordSubmit;
		_passwordCancelBtn.Pressed += () => _passwordDialog.Hide();

		_discordBtn.Pressed += () => OS.ShellOpen("https://discord.gg/dxvAvKg7FZ");

		// Blog Connections
		_prevBtn.Pressed += () => { if (_currentBlogIndex > 0) { _currentBlogIndex--; UpdateBlogDisplay(); } };
		_nextBtn.Pressed += () => { if (_currentBlogIndex < _blogs.Count - 1) { _currentBlogIndex++; UpdateBlogDisplay(); } };
		_centerClickBtn.Pressed += ShowFullArticle;
		_fullBlogCloseBtn.Pressed += () => _fullBlogDialog.Hide();
		
		// Initial State
		SetMode(false);
		
		// Load Blogs
		LoadBlogs();
		UpdateBlogDisplay();
	}

	private void LoadBlogs()
	{
		try
		{
			if (FileAccess.FileExists("res://blogs.json"))
			{
				using var file = FileAccess.Open("res://blogs.json", FileAccess.ModeFlags.Read);
				string jsonString = file.GetAsText();
				_blogs = JsonSerializer.Deserialize<List<BlogPost>>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("Failed to load blogs.json: " + e.Message);
		}

		if (_blogs == null || _blogs.Count == 0)
		{
			_blogs = new List<BlogPost> {
				new BlogPost { 
					Title = "No News", 
					Date = "N/A", 
					Excerpt = "Check back later for updates.", 
					Content = "There are no blog posts available at the moment." 
				}
			};
		}
	}

	private void UpdateBlogDisplay()
	{
		if (_blogs.Count == 0) return;

		// Left Panel
		if (_currentBlogIndex > 0)
		{
			_leftBlog.Show();
			_leftBlogTitle.Text = _blogs[_currentBlogIndex - 1].Title;
		}
		else
		{
			_leftBlog.Hide();
		}
		
		// Right Panel
		if (_currentBlogIndex < _blogs.Count - 1)
		{
			_rightBlog.Show();
			_rightBlogTitle.Text = _blogs[_currentBlogIndex + 1].Title;
		}
		else
		{
			_rightBlog.Hide();
		}
		
		// Center Panel
		var current = _blogs[_currentBlogIndex];
		_centerBlogTitle.Text = current.Title;
		_centerBlogDate.Text = current.Date;
		_centerBlogExcerpt.Text = current.Excerpt;
		
		_prevBtn.Visible = _currentBlogIndex > 0;
		_nextBtn.Visible = _currentBlogIndex < _blogs.Count - 1;
	}

	private void ShowFullArticle()
	{
		var current = _blogs[_currentBlogIndex];
		_fullBlogTitle.Text = current.Title;
		_fullBlogDate.Text = current.Date;
		_fullBlogContent.Text = current.Content;
		_fullBlogDialog.Show();
	}

	private void OnActionSelected(long index) => UpdateActionBtnText((int)index);

	private void UpdateActionBtnText(int index)
	{
		if (_isServerMode)
		{
			switch(index)
			{
				case 0: _clientActionBtn.Text = "INSTALL SERVER"; break;
				case 1: _clientActionBtn.Text = "UPDATE SERVER"; break;
			}
		}
		else
		{
			switch(index)
			{
				case 0: _clientActionBtn.Text = "INSTALL"; break;
				case 1: _clientActionBtn.Text = "UPDATE"; break;
				case 2: _clientActionBtn.Text = "PLAY"; break;
			}
		}
	}

	private void OnModeToggled(bool toggledOn)
	{
		if (toggledOn && !_isServerMode)
		{
			_passwordEdit.Text = "";
			_passwordDialog.Show();
			_modeToggle.SetPressedNoSignal(false);
		}
		else if (!toggledOn && _isServerMode)
		{
			SetMode(false);
		}
	}

	private void SetMode(bool serverMode)
	{
		if (_isServerMode) _serverSavedPath = _clientPathEdit.Text;
		else _clientSavedPath = _clientPathEdit.Text;

		_isServerMode = serverMode;
		_modeToggle.SetPressedNoSignal(serverMode);
		_modeToggle.Text = serverMode ? "SERVER" : "CLIENT";

		_clientPathEdit.Text = serverMode ? _serverSavedPath : _clientSavedPath;
		_clientStatus.Text = "Idle.";
		_clientProgress.ResetBar();

		_clientActionCombo.Clear();
		if (serverMode)
		{
			_clientActionCombo.AddItem("Install Server");
			_clientActionCombo.AddItem("Update Server");
		}
		else
		{
			_clientActionCombo.AddItem("Install");
			_clientActionCombo.AddItem("Update");
			_clientActionCombo.AddItem("Play");
		}
		
		UpdateActionBtnText(0);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.A && keyEvent.CtrlPressed && keyEvent.ShiftPressed)
			{
				_passwordEdit.Text = "";
				_passwordDialog.Show();
			}
		}
	}

	private void OnPasswordSubmit()
	{
		if (_passwordEdit.Text == "8008")
		{
			_passwordDialog.Hide();
			SetMode(true);
		}
		else
		{
			_passwordEdit.Text = "";
			GD.Print("Incorrect admin password.");
		}
	}

	private async void OnClientActionExecute()
	{
		if (_isServerMode)
		{
			await OnServerActionExecute();
			return;
		}

		int action = _clientActionCombo.Selected;
		string path = _clientPathEdit.Text;
		
		_clientActionBtn.Disabled = true;
		_clientActionCombo.Disabled = true;
		
		if (action == 0) // Install — full prerequisites + client
		{
			_clientStatus.Text = $"Installing to {path}...";
			_clientProgress.Value = 0;
			_clientProgress.BeginWork();

			bool prereqsOk = await EnsureClientPrerequisites(path);
			if (!prereqsOk)
			{
				_clientStatus.Text = "Install failed: prerequisites (.NET / Lantern).";
				_clientProgress.EndWorkFailed();
				_clientActionBtn.Disabled = false;
				_clientActionCombo.Disabled = false;
				return;
			}

			bool clientOk = await InstallOrUpdateClientFromGitHubRelease(path, action, progressUsesFullBar: false);
			if (!clientOk)
			{
				_clientStatus.Text = "Install failed: client download or extract.";
				_clientProgress.EndWorkFailed();
				_clientActionBtn.Disabled = false;
				_clientActionCombo.Disabled = false;
				return;
			}

			_clientProgress.Value = 100;
			_clientProgress.EndWorkSuccess();
			_clientStatus.Text = "Installation Complete.";
			
			// Switch to Play mode automatically
			_clientActionCombo.Selected = 2;
			UpdateActionBtnText(2);
		}
		else if (action == 1) // Update — only when an install already exists; no .NET installer pass
		{
			_clientStatus.Text = $"Updating at {path}...";
			string p = (path ?? string.Empty).Trim();

			if (string.IsNullOrWhiteSpace(p))
			{
				_clientStatus.Text = "Choose an install folder first.";
				_clientProgress.SetDownloadActive(false);
				_clientActionBtn.Disabled = false;
				_clientActionCombo.Disabled = false;
				return;
			}

			if (!System.IO.Directory.Exists(p))
			{
				_clientStatus.Text =
					"Nothing to update — that folder does not exist. Check the path, or use Install for a first-time setup.";
				_clientProgress.SetDownloadActive(false);
				_clientActionBtn.Disabled = false;
				_clientActionCombo.Disabled = false;
				return;
			}

			if (!LooksLikeInstalledClient(p))
			{
				_clientStatus.Text =
					"Nothing to update here — no EQ.gd install detected. Point the launcher at your game folder, or use Install.";
				_clientProgress.SetDownloadActive(false);
				_clientActionBtn.Disabled = false;
				_clientActionCombo.Disabled = false;
				return;
			}

			_clientProgress.Value = 0;
			_clientProgress.BeginWork();

			if (!System.IO.File.Exists(GetClientLanternExe(p)))
			{
				_clientStatus.Text = "Restoring LanternExtractor...";
				bool lanternOk = await EnsureLanternExtractorInstalled(p, 0, 20);
				if (!lanternOk)
				{
					_clientStatus.Text = "Update failed: could not restore LanternExtractor.";
					_clientProgress.EndWorkFailed();
					_clientActionBtn.Disabled = false;
					_clientActionCombo.Disabled = false;
					return;
				}
			}

			bool clientOkUpdate = await InstallOrUpdateClientFromGitHubRelease(p, action, progressUsesFullBar: true);
			if (!clientOkUpdate)
			{
				_clientStatus.Text = "Update failed: client download or extract.";
				_clientProgress.EndWorkFailed();
				_clientActionBtn.Disabled = false;
				_clientActionCombo.Disabled = false;
				return;
			}

			_clientProgress.Value = 100;
			_clientProgress.EndWorkSuccess();
			_clientStatus.Text = "Update Complete.";

			_clientActionCombo.Selected = 2;
			UpdateActionBtnText(2);
		}
		else if (action == 2) // Play
		{
			_clientProgress.SetDownloadActive(false);
			string gameExe = FindClientGameExe(path);
			if (string.IsNullOrEmpty(gameExe))
			{
				_clientStatus.Text = "Client not installed. Run Install first.";
			}
			else
			{
				_clientStatus.Text = "Launching game...";
				try
				{
					string workDir = System.IO.Path.GetDirectoryName(gameExe) ?? path;
					var psi = new System.Diagnostics.ProcessStartInfo
					{
						FileName = gameExe,
						WorkingDirectory = workDir,
						UseShellExecute = true
					};
					System.Diagnostics.Process.Start(psi);
					_clientStatus.Text = "Game launched.";
				}
				catch (Exception ex)
				{
					GD.PrintErr("[Launcher] Failed to start client: " + ex.Message);
					_clientStatus.Text = "Failed to launch game.";
				}
			}
		}

		_clientActionBtn.Disabled = false;
		_clientActionCombo.Disabled = false;
	}

	private async Task OnServerActionExecute()
	{
		int action = _clientActionCombo.Selected;
		string path = _clientPathEdit.Text;
		
		_clientActionBtn.Disabled = true;
		_clientActionCombo.Disabled = true;
		
		_clientStatus.Text = action == 0 ? $"Installing server to {path}..." : $"Updating server at {path}...";

		if (string.IsNullOrWhiteSpace(path))
		{
			_clientStatus.Text = "Choose an install folder first.";
			_clientActionBtn.Disabled = false;
			_clientActionCombo.Disabled = false;
			return;
		}

		_clientProgress.Value = 0;
		_clientProgress.BeginWork();

		bool serverOk = await InstallOrUpdateServerFromGitHubRelease(path, action);
		if (!serverOk)
		{
			_clientStatus.Text = action == 0 ? "Server install failed." : "Server update failed.";
			_clientProgress.EndWorkFailed();
			_clientActionBtn.Disabled = false;
			_clientActionCombo.Disabled = false;
			return;
		}

		_clientProgress.Value = 100;
		_clientProgress.EndWorkSuccess();
		_clientStatus.Text = action == 0 ? "Server Installation Complete." : "Server Update Complete.";
		
		_clientActionBtn.Disabled = false;
		_clientActionCombo.Disabled = false;
	}

	private void SetClientProgressDeferred(int value) =>
		Callable.From(() => _clientProgress.Value = Math.Clamp(value, 0, 100)).CallDeferred();

	private void SetClientStatusDeferred(string text) =>
		Callable.From(() => _clientStatus.Text = text).CallDeferred();

	private async Task YieldToMainThread()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private async Task<bool> EnsureClientPrerequisites(string installPath)
	{
		await YieldToMainThread();

		if (!OperatingSystem.IsWindows())
		{
			SetClientStatusDeferred("Non-Windows: install .NET 8 + 6 runtimes manually if needed.");
			return await EnsureLanternExtractorInstalled(installPath, 50, 85);
		}

		_clientProgress.Value = 0;

		if (!await EnsureDotNetCoreRuntimeInstalled(
				ClientNetMajor,
				DotNet8DesktopInstallerUrl,
				"Downloading .NET 8 Windows Desktop runtime (for EQ.gd)...",
				0,
				25))
			return false;

		if (!await EnsureDotNetCoreRuntimeInstalled(
				LanternNetMajor,
				DotNet6RuntimeInstallerUrl,
				"Downloading .NET 6 runtime (for LanternExtractor)...",
				25,
				50))
			return false;

		return await EnsureLanternExtractorInstalled(installPath, 50, 85);
	}

	private static bool HasDotNetSharedMajorInstalled(int major)
	{
		if (HasSharedFrameworkDirInProgramFiles("Microsoft.NETCore.App", major))
			return true;
		if (HasSharedFrameworkDirInProgramFiles("Microsoft.WindowsDesktop.App", major))
			return true;
		return TryHasSharedFrameworkViaDotNetCli(major);
	}

	private static bool HasSharedFrameworkDirInProgramFiles(string frameworkName, int major)
	{
		try
		{
			string pf = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles);
			string fxDir = System.IO.Path.Combine(pf, "dotnet", "shared", frameworkName);
			if (!System.IO.Directory.Exists(fxDir))
				return false;

			string prefix = $"{major}.";
			foreach (var dir in System.IO.Directory.GetDirectories(fxDir))
			{
				string ver = System.IO.Path.GetFileName(dir);
				if (ver.StartsWith(prefix, StringComparison.Ordinal))
					return true;
			}
		}
		catch
		{
			// ignore
		}

		return false;
	}

	private static bool TryHasSharedFrameworkViaDotNetCli(int major)
	{
		try
		{
			string pf = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles);
			string dotnetExe = System.IO.Path.Combine(pf, "dotnet", "dotnet.exe");
			if (!System.IO.File.Exists(dotnetExe))
				return false;

			var psi = new System.Diagnostics.ProcessStartInfo
			{
				FileName = dotnetExe,
				Arguments = "--list-runtimes",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};

			using var p = System.Diagnostics.Process.Start(psi);
			if (p == null)
				return false;

			string output = p.StandardOutput.ReadToEnd();
			p.WaitForExit(20000);

			string coreNeedle = $"Microsoft.NETCore.App {major}.";
			string desktopNeedle = $"Microsoft.WindowsDesktop.App {major}.";
			return output.Contains(coreNeedle, StringComparison.Ordinal)
				|| output.Contains(desktopNeedle, StringComparison.Ordinal);
		}
		catch
		{
			return false;
		}
	}

	private async Task<int> RunInstallerElevatedQuietAsync(string installerExePath)
	{
		return await Task.Run(() =>
		{
			var psi = new System.Diagnostics.ProcessStartInfo
			{
				FileName = installerExePath,
				Arguments = "/quiet /norestart",
				UseShellExecute = true,
				Verb = "runas"
			};

			using var p = System.Diagnostics.Process.Start(psi);
			if (p == null)
				return -1;

			p.WaitForExit(600000);
			return p.ExitCode;
		});
	}

	private async Task<bool> EnsureDotNetCoreRuntimeInstalled(
		int major,
		string installerUrl,
		string downloadStatus,
		int progressStart,
		int progressEnd)
	{
		await YieldToMainThread();

		if (HasDotNetSharedMajorInstalled(major))
		{
			_clientStatus.Text = $".NET {major} runtime already installed.";
			_clientProgress.Value = progressEnd;
			return true;
		}

		_clientStatus.Text = downloadStatus;
		string tmpInstaller = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"EQMUD_dotnet{major}_runtime_" + Guid.NewGuid().ToString("N") + ".exe");

		try
		{
			await DownloadFileAsync(installerUrl, tmpInstaller, (downloaded, total) =>
			{
				int lo = progressStart;
				int hi = progressEnd - 6;
				if (total > 0)
				{
					float pct = (float)downloaded / total;
					int v = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(lo, hi, pct)), lo, hi);
					SetClientProgressDeferred(v);
				}
				else
				{
					SetClientProgressDeferred(Mathf.Min(hi, lo + 1));
				}
			});

			await YieldToMainThread();
			_clientStatus.Text = $"Installing .NET {major} runtime (UAC may appear)...";
			_clientProgress.Value = progressEnd - 5;

			int exit = await RunInstallerElevatedQuietAsync(tmpInstaller);
			if (exit != 0 && exit != 3010)
			{
				GD.PrintErr($"[Launcher] .NET {major} installer exited with code {exit}.");
				_clientStatus.Text = $".NET {major} install failed (exit {exit}).";
				return false;
			}

			if (exit == 3010)
				GD.Print("[Launcher] .NET installer requested a reboot (exit 3010). Continuing.");

			for (int i = 0; i < 8; i++)
			{
				await YieldToMainThread();
				if (HasDotNetSharedMajorInstalled(major))
				{
					_clientProgress.Value = progressEnd;
					_clientStatus.Text = $".NET {major} runtime installed.";
					return true;
				}

				await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
			}

			GD.PrintErr($"[Launcher] .NET {major} runtime not detected after install.");
			_clientStatus.Text = $".NET {major} install finished but runtime not detected.";
			return false;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[Launcher] .NET {major} install error: {ex.Message}");
			_clientStatus.Text = $".NET {major} install failed.";
			return false;
		}
		finally
		{
			try { System.IO.File.Delete(tmpInstaller); } catch { }
		}
	}

	private string GetClientLanternDir(string installPath) =>
		System.IO.Path.Combine(installPath, "LanternExtractor");

	private string GetClientLanternExe(string installPath) =>
		System.IO.Path.Combine(GetClientLanternDir(installPath), LanternExtractorExeName);

	private async Task<bool> EnsureLanternExtractorInstalled(string installPath, int progressStart, int progressEnd)
	{
		if (string.IsNullOrWhiteSpace(installPath))
		{
			GD.PrintErr("[Launcher] Client install path is empty.");
			return false;
		}

		System.IO.Directory.CreateDirectory(installPath);

		string lanternDir = GetClientLanternDir(installPath);
		string lanternExe = GetClientLanternExe(installPath);

		void SetLanternProgress(float t01)
		{
			int v = Mathf.Clamp(
				Mathf.RoundToInt(Mathf.Lerp(progressStart, progressEnd, Mathf.Clamp(t01, 0f, 1f))),
				progressStart,
				progressEnd);
			SetClientProgressDeferred(v);
		}

		// Already present?
		if (System.IO.File.Exists(lanternExe))
		{
			_clientStatus.Text = "LanternExtractor already installed.";
			SetLanternProgress(1f);
			return true;
		}

		_clientStatus.Text = "Downloading LanternExtractor...";
		SetLanternProgress(0.05f);

		// Download to a temp file, then extract into the install folder.
		string tmpRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "EQMUD_LanternExtractor_" + Guid.NewGuid().ToString("N"));
		System.IO.Directory.CreateDirectory(tmpRoot);

		string zipPath = System.IO.Path.Combine(tmpRoot, "LanternExtractor.zip");

		try
		{
			await DownloadFileAsync(LanternExtractorZipUrl, zipPath, (downloaded, total) =>
			{
				if (total > 0)
				{
					float pct = (float)downloaded / total;
					SetLanternProgress(0.05f + 0.55f * pct);
				}
				else
				{
					SetLanternProgress(0.35f);
				}
			});

			await YieldToMainThread();
			_clientStatus.Text = "Extracting LanternExtractor...";
			SetLanternProgress(0.7f);

			System.IO.Directory.CreateDirectory(lanternDir);

			// Extract zip into temp, then copy the folder that contains LanternExtractor.exe.
			ZipFile.ExtractToDirectory(zipPath, tmpRoot, overwriteFiles: true);

			string extractedExe = FindFirstFile(tmpRoot, LanternExtractorExeName);
			if (string.IsNullOrWhiteSpace(extractedExe) || !System.IO.File.Exists(extractedExe))
			{
				GD.PrintErr("[Launcher] LanternExtractor.exe not found after extraction.");
				return false;
			}

			string sourceDir = System.IO.Path.GetDirectoryName(extractedExe) ?? tmpRoot;

			// Copy everything alongside the exe (e.g. ClientData).
			CopyDirectory(sourceDir, lanternDir);

			SetLanternProgress(0.95f);
			_clientStatus.Text = "LanternExtractor installed.";
			return System.IO.File.Exists(lanternExe);
		}
		catch (Exception ex)
		{
			GD.PrintErr("[Launcher] LanternExtractor install failed: " + ex.Message);
			_clientStatus.Text = "LanternExtractor download/extract failed.";
			return false;
		}
		finally
		{
			// Best-effort cleanup; failures are non-fatal.
			try { System.IO.Directory.Delete(tmpRoot, recursive: true); } catch { }
		}
	}

	private static string GetClientStateDir(string installPath) =>
		System.IO.Path.Combine(installPath.TrimEnd(), ".eqgd");

	private static string GetClientStateFilePath(string installPath) =>
		System.IO.Path.Combine(GetClientStateDir(installPath), "client_state.json");

	private static bool TryReadClientInstallState(string installPath, out ClientInstallState state)
	{
		state = null;
		try
		{
			string path = GetClientStateFilePath(installPath);
			if (!System.IO.File.Exists(path))
				return false;

			string json = System.IO.File.ReadAllText(path);
			var parsed = JsonSerializer.Deserialize<ClientInstallState>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			if (parsed == null)
				return false;
			state = parsed;
			return true;
		}
		catch
		{
			state = null;
			return false;
		}
	}

	private static void WriteClientInstallState(string installPath, ClientReleaseZipAsset asset)
	{
		string dir = GetClientStateDir(installPath);
		System.IO.Directory.CreateDirectory(dir);

		var state = new ClientInstallState
		{
			Tag = asset.ReleaseTag,
			Asset = asset.Name,
			Sha256 = asset.Sha256Hex ?? string.Empty
		};

		var writeOpts = new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};
		string json = JsonSerializer.Serialize(state, writeOpts);
		System.IO.File.WriteAllText(GetClientStateFilePath(installPath), json);
	}

	private static string GetServerStateDir(string installPath) =>
		System.IO.Path.Combine(installPath.TrimEnd(), ".eqmud_server");

	private static string GetServerStateFilePath(string installPath) =>
		System.IO.Path.Combine(GetServerStateDir(installPath), "server_state.json");

	private static bool TryReadServerInstallState(string installPath, out ClientInstallState state)
	{
		state = null;
		try
		{
			string path = GetServerStateFilePath(installPath);
			if (!System.IO.File.Exists(path))
				return false;

			string json = System.IO.File.ReadAllText(path);
			var parsed = JsonSerializer.Deserialize<ClientInstallState>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			if (parsed == null)
				return false;
			state = parsed;
			return true;
		}
		catch
		{
			state = null;
			return false;
		}
	}

	private static void WriteServerInstallState(string installPath, ClientReleaseZipAsset asset)
	{
		string dir = GetServerStateDir(installPath);
		System.IO.Directory.CreateDirectory(dir);

		var state = new ClientInstallState
		{
			Tag = asset.ReleaseTag,
			Asset = asset.Name,
			Sha256 = asset.Sha256Hex ?? string.Empty
		};

		var writeOpts = new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};
		string json = JsonSerializer.Serialize(state, writeOpts);
		System.IO.File.WriteAllText(GetServerStateFilePath(installPath), json);
	}

	private async Task<ClientReleaseZipAsset> FetchClientReleaseZipAssetRequiredAsync(string apiUrl, bool preferServer)
	{
		using var http = new System.Net.Http.HttpClient();
		http.DefaultRequestHeaders.UserAgent.ParseAdd("EQMUD-Launcher/0.1 (+https://github.com/KaelKodes/Everquest-Godot-Launcher)");
		http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
		http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
		http.Timeout = System.TimeSpan.FromMinutes(2);

		string json = await http.GetStringAsync(apiUrl);
		using JsonDocument doc = JsonDocument.Parse(json);
		JsonElement root = doc.RootElement;

		string tag = root.GetProperty("tag_name").GetString() ?? "latest";

		if (!root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
			throw new System.InvalidOperationException("GitHub release has no assets array.");

		// First pass: look for a specific server/client named zip if needed.
		if (preferServer)
		{
			foreach (JsonElement el in assets.EnumerateArray())
			{
				string name = el.GetProperty("name").GetString() ?? string.Empty;
				if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
					continue;
				if (name.Contains("server", StringComparison.OrdinalIgnoreCase))
					return ParseAsset(el, tag);
			}
		}
		else
		{
			foreach (JsonElement el in assets.EnumerateArray())
			{
				string name = el.GetProperty("name").GetString() ?? string.Empty;
				if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
					continue;
				if (name.Contains("client", StringComparison.OrdinalIgnoreCase))
					return ParseAsset(el, tag);
			}
		}

		// Fallback: Pick the first .zip we find on the latest release.
		foreach (JsonElement el in assets.EnumerateArray())
		{
			string name = el.GetProperty("name").GetString() ?? string.Empty;
			if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
				continue;

			return ParseAsset(el, tag);
		}

		throw new System.InvalidOperationException("No .zip asset found on latest GitHub release.");
	}

	private ClientReleaseZipAsset ParseAsset(JsonElement el, string tag)
	{
		string name = el.GetProperty("name").GetString() ?? string.Empty;
		string url = el.TryGetProperty("browser_download_url", out JsonElement bdu)
			? bdu.GetString() ?? string.Empty
			: string.Empty;
		long size = el.TryGetProperty("size", out JsonElement sz) ? sz.GetInt64() : -1;

		string shaHex = string.Empty;
		if (el.TryGetProperty("digest", out JsonElement dig))
		{
			string d = dig.GetString() ?? string.Empty;
			const string prefix = "sha256:";
			if (d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				shaHex = d.Substring(prefix.Length).Trim();
		}

		return new ClientReleaseZipAsset
		{
			ReleaseTag = tag,
			Name = name,
			BrowserDownloadUrl = url,
			Size = size,
			Sha256Hex = shaHex
		};
	}

	private async Task<bool> InstallOrUpdateClientFromGitHubRelease(string installPath, int action, bool progressUsesFullBar = false)
	{
		await YieldToMainThread();

		installPath = installPath?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(installPath))
			return false;

		System.IO.Directory.CreateDirectory(installPath);

		int dlStart = progressUsesFullBar ? 8 : 85;
		int dlEnd = progressUsesFullBar ? 88 : 94;
		int verifyProgress = progressUsesFullBar ? 89 : 94;
		int extractStart = progressUsesFullBar ? 90 : 95;
		int extractEnd = progressUsesFullBar ? 99 : 99;

		ClientReleaseZipAsset asset;
		try
		{
			asset = await FetchClientReleaseZipAssetRequiredAsync(ClientGithubReleaseApiUrl, preferServer: false);
		}
		catch (Exception ex)
		{
			GD.PrintErr("[Launcher] Failed to read GitHub release: " + ex.Message);
			_clientStatus.Text = "Could not read client release from GitHub.";
			return false;
		}

		if (string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
		{
			GD.PrintErr("[Launcher] Release zip has no download URL.");
			return false;
		}

		if (action == 1
			&& TryReadClientInstallState(installPath, out ClientInstallState existing)
			&& string.Equals(existing.Tag, asset.ReleaseTag, StringComparison.Ordinal)
			&& string.Equals(existing.Asset, asset.Name, StringComparison.Ordinal)
			&& !string.IsNullOrWhiteSpace(asset.Sha256Hex)
			&& string.Equals(existing.Sha256, asset.Sha256Hex, StringComparison.OrdinalIgnoreCase)
			&& !string.IsNullOrEmpty(FindClientGameExe(installPath)))
		{
			_clientStatus.Text = "Client already up to date.";
			_clientProgress.SetDownloadActive(false);
			_clientProgress.Value = 100;
			return true;
		}

		string tempZip = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			"EQMUD_client_" + Guid.NewGuid().ToString("N") + ".zip");

		string extractTmp = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			"EQMUD_client_extract_" + Guid.NewGuid().ToString("N"));

		try
		{
			_clientStatus.Text = $"Downloading {asset.Name}...";

			await DownloadFileAsync(asset.BrowserDownloadUrl, tempZip, (downloaded, total) =>
			{
				int lo = dlStart;
				int hi = dlEnd;
				if (total > 0)
				{
					float pct = (float)downloaded / total;
					int v = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(lo, hi, pct)), lo, hi);
					SetClientProgressDeferred(v);
				}
				else
				{
					SetClientProgressDeferred(lo + 1);
				}
			});

			await YieldToMainThread();

			if (!string.IsNullOrWhiteSpace(asset.Sha256Hex))
			{
				_clientStatus.Text = "Verifying client download...";
				_clientProgress.Value = verifyProgress;
				bool okHash = await VerifyFileSha256Async(tempZip, asset.Sha256Hex);
				if (!okHash)
				{
					GD.PrintErr("[Launcher] Client zip SHA-256 mismatch.");
					_clientStatus.Text = "Download verification failed (hash).";
					return false;
				}
			}

			await YieldToMainThread();
			_clientStatus.Text = "Extracting client...";
			_clientProgress.Value = extractStart;

			System.IO.Directory.CreateDirectory(extractTmp);
			await ExtractZipSafeWithProgressAsync(tempZip, extractTmp, extractStart, extractEnd);

			string contentRoot = ResolveExtractedClientRoot(extractTmp);
			CopyDirectory(contentRoot, installPath);

			WriteClientInstallState(installPath, asset);

			string exe = FindClientGameExe(installPath);
			if (string.IsNullOrEmpty(exe))
			{
				GD.PrintErr("[Launcher] Client installed but no game .exe found next to install path.");
				_clientStatus.Text = "Installed, but game .exe not found.";
				return false;
			}

			return true;
		}
		catch (Exception ex)
		{
			GD.PrintErr("[Launcher] Client install failed: " + ex.Message);
			_clientStatus.Text = "Client install failed.";
			return false;
		}
		finally
		{
			try { System.IO.File.Delete(tempZip); } catch { }
			try { System.IO.Directory.Delete(extractTmp, true); } catch { }
		}
	}

	private async Task<bool> InstallOrUpdateServerFromGitHubRelease(string installPath, int action)
	{
		await YieldToMainThread();

		installPath = installPath?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(installPath))
			return false;

		System.IO.Directory.CreateDirectory(installPath);

		int dlStart = 5;
		int dlEnd = 85;
		int verifyProgress = 86;
		int extractStart = 88;
		int extractEnd = 99;

		ClientReleaseZipAsset asset;
		try
		{
			asset = await FetchClientReleaseZipAssetRequiredAsync(ServerGithubReleaseApiUrl, preferServer: true);
		}
		catch (Exception ex)
		{
			GD.PrintErr("[Launcher] Failed to read server GitHub release: " + ex.Message);
			_clientStatus.Text = "Could not read server release from GitHub.";
			return false;
		}

		if (string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
		{
			GD.PrintErr("[Launcher] Server release zip has no download URL.");
			return false;
		}

		if (action == 1
			&& TryReadServerInstallState(installPath, out ClientInstallState existing)
			&& string.Equals(existing.Tag, asset.ReleaseTag, StringComparison.Ordinal)
			&& string.Equals(existing.Asset, asset.Name, StringComparison.Ordinal)
			&& !string.IsNullOrWhiteSpace(asset.Sha256Hex)
			&& string.Equals(existing.Sha256, asset.Sha256Hex, StringComparison.OrdinalIgnoreCase))
		{
			_clientStatus.Text = "Server already up to date.";
			_clientProgress.SetDownloadActive(false);
			_clientProgress.Value = 100;
			return true;
		}

		string tempZip = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			"EQMUD_server_" + Guid.NewGuid().ToString("N") + ".zip");

		string extractTmp = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			"EQMUD_server_extract_" + Guid.NewGuid().ToString("N"));

		try
		{
			_clientStatus.Text = $"Downloading {asset.Name}...";

			await DownloadFileAsync(asset.BrowserDownloadUrl, tempZip, (downloaded, total) =>
			{
				int lo = dlStart;
				int hi = dlEnd;
				if (total > 0)
				{
					float pct = (float)downloaded / total;
					int v = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(lo, hi, pct)), lo, hi);
					SetClientProgressDeferred(v);
				}
				else
				{
					SetClientProgressDeferred(lo + 1);
				}
			});

			await YieldToMainThread();

			if (!string.IsNullOrWhiteSpace(asset.Sha256Hex))
			{
				_clientStatus.Text = "Verifying server download...";
				_clientProgress.Value = verifyProgress;
				bool okHash = await VerifyFileSha256Async(tempZip, asset.Sha256Hex);
				if (!okHash)
				{
					GD.PrintErr("[Launcher] Server zip SHA-256 mismatch.");
					_clientStatus.Text = "Download verification failed (hash).";
					return false;
				}
			}

			await YieldToMainThread();
			_clientStatus.Text = "Extracting server...";
			_clientProgress.Value = extractStart;

			System.IO.Directory.CreateDirectory(extractTmp);
			await ExtractZipSafeWithProgressAsync(tempZip, extractTmp, extractStart, extractEnd, isServer: true);

			string contentRoot = ResolveExtractedClientRoot(extractTmp);
			CopyDirectory(contentRoot, installPath);

			WriteServerInstallState(installPath, asset);

			return true;
		}
		catch (Exception ex)
		{
			GD.PrintErr("[Launcher] Server install failed: " + ex.Message);
			_clientStatus.Text = "Server install failed.";
			return false;
		}
		finally
		{
			try { System.IO.File.Delete(tempZip); } catch { }
			try { System.IO.Directory.Delete(extractTmp, true); } catch { }
		}
	}

	private static async Task<bool> VerifyFileSha256Async(string filePath, string expectedHex)
	{
		expectedHex = (expectedHex ?? string.Empty).Trim();
		if (expectedHex.Length == 0)
			return true;

		return await Task.Run(() =>
		{
			using var sha = SHA256.Create();
			using var fs = System.IO.File.OpenRead(filePath);
			byte[] hash = sha.ComputeHash(fs);
			string actual = Convert.ToHexString(hash);
			return string.Equals(actual, expectedHex, StringComparison.OrdinalIgnoreCase);
		});
	}

	private async Task ExtractZipSafeWithProgressAsync(string zipPath, string destDir, int progressStart, int progressEnd, bool isServer = false)
	{
		await Task.Run(() =>
		{
			using var fs = System.IO.File.OpenRead(zipPath);
			using var archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);

			long totalBytes = 0;
			foreach (ZipArchiveEntry entry in archive.Entries)
			{
				if (string.IsNullOrEmpty(entry.Name))
					continue;
				totalBytes += entry.Length;
			}

			long doneBytes = 0;
			int entryIndex = 0;
			foreach (ZipArchiveEntry entry in archive.Entries)
			{
				if (string.IsNullOrEmpty(entry.Name))
					continue;

				string destPath = GetSafeZipEntryPath(destDir, entry.FullName);
				string parent = System.IO.Path.GetDirectoryName(destPath);
				if (!string.IsNullOrEmpty(parent))
					System.IO.Directory.CreateDirectory(parent);

				entry.ExtractToFile(destPath, overwrite: true);

				doneBytes += entry.Length;
				entryIndex++;

				if (totalBytes > 0 && (entryIndex % 64 == 0 || doneBytes == totalBytes))
				{
					float t = (float)doneBytes / totalBytes;
					int v = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(progressStart, progressEnd, t)), progressStart, progressEnd);
					if (isServer)
						Callable.From(() => _clientProgress.Value = v).CallDeferred();
					else
						Callable.From(() => _clientProgress.Value = v).CallDeferred();
				}
			}
		});

		await YieldToMainThread();
	}

	private static string GetSafeZipEntryPath(string destDir, string entryFullName)
	{
		string root = System.IO.Path.GetFullPath(destDir);
		string cleaned = (entryFullName ?? string.Empty).Replace('\\', '/').TrimStart('/');
		string[] parts = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries);

		string combined = root;
		foreach (string part in parts)
		{
			if (part == ".")
				continue;
			if (part == "..")
				throw new System.IO.InvalidDataException("Blocked zip path traversal: " + entryFullName);
			combined = System.IO.Path.GetFullPath(System.IO.Path.Combine(combined, part));
		}

		string destRoot = root;
		if (!destRoot.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
			destRoot += System.IO.Path.DirectorySeparatorChar;

		string rootTrim = root.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
		if (!combined.Equals(rootTrim, StringComparison.OrdinalIgnoreCase)
			&& !combined.StartsWith(destRoot, StringComparison.OrdinalIgnoreCase))
		{
			throw new System.IO.InvalidDataException("Blocked zip path traversal: " + entryFullName);
		}

		return combined;
	}

	private static string ResolveExtractedClientRoot(string extractTmp)
	{
		string[] topFiles = System.IO.Directory.GetFiles(extractTmp);
		string[] topDirs = System.IO.Directory.GetDirectories(extractTmp);

		if (topDirs.Length == 1 && topFiles.Length == 0)
			return topDirs[0];

		return extractTmp;
	}

	private static bool LooksLikeInstalledClient(string installPath)
	{
		installPath = installPath?.Trim() ?? string.Empty;
		if (installPath.Length == 0 || !System.IO.Directory.Exists(installPath))
			return false;

		if (!string.IsNullOrEmpty(FindClientGameExe(installPath)))
			return true;

		return System.IO.File.Exists(GetClientStateFilePath(installPath));
	}

	private static string FindClientGameExe(string installPath)
	{
		installPath = installPath?.Trim() ?? string.Empty;
		if (installPath.Length == 0)
			return string.Empty;

		if (!System.IO.Directory.Exists(installPath))
			return string.Empty;

		foreach (string path in System.IO.Directory.EnumerateFiles(installPath, "*.exe", System.IO.SearchOption.TopDirectoryOnly))
		{
			if (IsNonGameExe(path))
				continue;
			return path;
		}

		foreach (string dir in System.IO.Directory.EnumerateDirectories(installPath))
		{
			string dirName = System.IO.Path.GetFileName(dir);
			if (dirName.Equals("LanternExtractor", StringComparison.OrdinalIgnoreCase))
				continue;
			if (dirName.Equals(".eqgd", StringComparison.OrdinalIgnoreCase))
				continue;

			foreach (string path in System.IO.Directory.EnumerateFiles(dir, "*.exe", System.IO.SearchOption.TopDirectoryOnly))
			{
				if (IsNonGameExe(path))
					continue;
				return path;
			}
		}

		return string.Empty;
	}

	private static bool IsNonGameExe(string fullPath)
	{
		string name = System.IO.Path.GetFileName(fullPath);
		if (name.Equals("LanternExtractor.exe", StringComparison.OrdinalIgnoreCase))
			return true;
		if (name.StartsWith("unins", StringComparison.OrdinalIgnoreCase))
			return true;
		return false;
	}

	private static string FindFirstFile(string rootDir, string fileName)
	{
		foreach (var file in System.IO.Directory.EnumerateFiles(rootDir, fileName, System.IO.SearchOption.AllDirectories))
			return file;
		return string.Empty;
	}

	private async Task DownloadFileAsync(string url, string destFile, Action<long, long> onProgress)
	{
		using var http = new System.Net.Http.HttpClient();
		http.DefaultRequestHeaders.UserAgent.ParseAdd("EQMUD-Launcher/0.1 (+https://github.com/KaelKodes/Everquest-Godot-Launcher)");
		http.DefaultRequestHeaders.Accept.ParseAdd("*/*");
		http.Timeout = System.TimeSpan.FromHours(4);

		GD.Print($"[Launcher] Download start: {url}");

		using var resp = await http.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
		resp.EnsureSuccessStatusCode();

		long total = resp.Content.Headers.ContentLength ?? -1;
		if (total > 0)
			GD.Print($"[Launcher] Content-Length: {total} bytes");

		await using var remoteStream = await resp.Content.ReadAsStreamAsync();
		await using var fileStream = new System.IO.FileStream(destFile, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);

		byte[] buffer = new byte[1024 * 128];
		long downloaded = 0;
		int read;
		int chunks = 0;
		while ((read = await remoteStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
		{
			await fileStream.WriteAsync(buffer.AsMemory(0, read));
			downloaded += read;
			onProgress?.Invoke(downloaded, total);

			// Let Godot repaint; long downloads otherwise look frozen.
			chunks++;
			if ((chunks & 7) == 0)
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		GD.Print($"[Launcher] Download done: {downloaded} bytes -> {destFile}");
	}

	private static void CopyDirectory(string sourceDir, string destDir)
	{
		System.IO.Directory.CreateDirectory(destDir);

		foreach (var file in System.IO.Directory.GetFiles(sourceDir))
		{
			string destFile = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(file));
			System.IO.File.Copy(file, destFile, overwrite: true);
		}

		foreach (var dir in System.IO.Directory.GetDirectories(sourceDir))
		{
			string destSub = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(dir));
			CopyDirectory(dir, destSub);
		}
	}
}
