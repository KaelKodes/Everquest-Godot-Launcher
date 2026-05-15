import sys
import re

file_path = r"d:\Kael Kodes\EQMUD\Launcher\EQ GD Launcher\LauncherMain.cs"

with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# Replace all occurrences
content = content.replace("_serverStatus", "_clientStatus")
content = content.replace("_serverProgress", "_clientProgress")
content = content.replace("_serverActionBtn", "_clientActionBtn")
content = content.replace("_serverActionCombo", "_clientActionCombo")
content = content.replace("_serverPathEdit", "_clientPathEdit")
content = content.replace("SetServerProgressDeferred", "SetClientProgressDeferred")
content = content.replace("SetServerStatusDeferred", "SetClientStatusDeferred")

# Also replace OnClientActionSelected and OnServerActionSelected
# We need to inject the SetMode and OnModeToggled
# Wait, I can just use regex for the block that I tried to replace manually

block1_target = r"""	private void OnClientActionSelected\(long index\) => UpdateClientActionBtnText\(\(int\)index\);
	private void OnServerActionSelected\(long index\) => UpdateServerActionBtnText\(\(int\)index\);

	private void UpdateClientActionBtnText\(int index\)
	\{
		switch\(index\)
		\{
			case 0: _clientActionBtn\.Text = "INSTALL"; break;
			case 1: _clientActionBtn\.Text = "UPDATE"; break;
			case 2: _clientActionBtn\.Text = "PLAY"; break;
		\}
	\}

	private void UpdateServerActionBtnText\(int index\)
	\{
		switch\(index\)
		\{
			case 0: _clientActionBtn\.Text = "INSTALL SERVER"; break;
			case 1: _clientActionBtn\.Text = "UPDATE SERVER"; break;
		\}
	\}"""

block1_replacement = r"""	private void OnActionSelected(long index) => UpdateActionBtnText((int)index);

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
	}"""

content = re.sub(block1_target, block1_replacement, content)

# _serverPanel.Show() needs to be replaced with SetMode(true);
content = content.replace("			_serverPanel.Show();", "			SetMode(true);")

# OnClientActionExecute needs to call OnServerActionExecute
block2_target = r"""	private async void OnClientActionExecute\(\)
	\{
		int action = _clientActionCombo\.Selected;"""

block2_replacement = r"""	private async void OnClientActionExecute()
	{
		if (_isServerMode)
		{
			await OnServerActionExecute();
			return;
		}

		int action = _clientActionCombo.Selected;"""

content = re.sub(block2_target, block2_replacement, content)

# Change OnServerActionExecute to Task
content = content.replace("private async void OnServerActionExecute()", "private async Task OnServerActionExecute()")

# Remove duplicate SetClientStatusDeferred/SetClientProgressDeferred
# because we replaced SetServer... with SetClient...
content = re.sub(r"""	private void SetClientProgressDeferred\(int value\) =>
		Callable\.From\(\(\) => _clientProgress\.Value = Math\.Clamp\(value, 0, 100\)\)\.CallDeferred\(\);

	private void SetClientStatusDeferred\(string text\) =>
		Callable\.From\(\(\) => _clientStatus\.Text = text\)\.CallDeferred\(\);

	private void SetClientProgressDeferred\(int value\) =>
		Callable\.From\(\(\) => _clientProgress\.Value = Math\.Clamp\(value, 0, 100\)\)\.CallDeferred\(\);

	private void SetClientStatusDeferred\(string text\) =>
		Callable\.From\(\(\) => _clientStatus\.Text = text\)\.CallDeferred\(\);""", 
r"""	private void SetClientProgressDeferred(int value) =>
		Callable.From(() => _clientProgress.Value = Math.Clamp(value, 0, 100)).CallDeferred();

	private void SetClientStatusDeferred(string text) =>
		Callable.From(() => _clientStatus.Text = text).CallDeferred();""", content)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

print("Patch applied.")
