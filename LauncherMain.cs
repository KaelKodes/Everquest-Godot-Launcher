using Godot;
using System;
using System.Collections.Generic;
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
    private OptionButton _clientActionCombo;
    private LineEdit _clientPathEdit;
    private Button _clientBrowseBtn;
    private Button _clientActionBtn;
    private ProgressBar _clientProgress;
    private Label _clientStatus;
    
    private Panel _passwordDialog;
    private LineEdit _passwordEdit;
    private Button _passwordSubmitBtn;
    private Button _passwordCancelBtn;
    
    private Panel _serverPanel;
    private OptionButton _serverActionCombo;
    private LineEdit _serverPathEdit;
    private Button _serverBrowseBtn;
    private Button _serverActionBtn;
    private ProgressBar _serverProgress;
    private Label _serverStatus;
    private Button _serverCloseBtn;
    
    private FileDialog _clientFolderDialog;
    private FileDialog _serverFolderDialog;

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

    public override void _Ready()
    {
        // Client UI
        _clientActionCombo = GetNode<OptionButton>("ClientUI/ActionCombo");
        _clientPathEdit = GetNode<LineEdit>("ClientUI/PathEdit");
        _clientBrowseBtn = GetNode<Button>("ClientUI/BrowseBtn");
        _clientActionBtn = GetNode<Button>("ClientUI/ActionBtn");
        _clientProgress = GetNode<ProgressBar>("ClientUI/ProgressBar");
        _clientStatus = GetNode<Label>("ClientUI/StatusLabel");
        
        // Password Dialog
        _passwordDialog = GetNode<Panel>("PasswordDialog");
        _passwordEdit = GetNode<LineEdit>("PasswordDialog/PasswordEdit");
        _passwordSubmitBtn = GetNode<Button>("PasswordDialog/SubmitBtn");
        _passwordCancelBtn = GetNode<Button>("PasswordDialog/CancelBtn");
        
        // Server UI
        _serverPanel = GetNode<Panel>("ServerPanel");
        _serverActionCombo = GetNode<OptionButton>("ServerPanel/ActionCombo");
        _serverPathEdit = GetNode<LineEdit>("ServerPanel/PathEdit");
        _serverBrowseBtn = GetNode<Button>("ServerPanel/BrowseBtn");
        _serverActionBtn = GetNode<Button>("ServerPanel/ActionBtn");
        _serverProgress = GetNode<ProgressBar>("ServerPanel/ProgressBar");
        _serverStatus = GetNode<Label>("ServerPanel/StatusLabel");
        _serverCloseBtn = GetNode<Button>("ServerPanel/CloseBtn");
        
        // File Dialogs
        _clientFolderDialog = GetNode<FileDialog>("ClientFolderDialog");
        _serverFolderDialog = GetNode<FileDialog>("ServerFolderDialog");

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

        // Setup Client Combo
        _clientActionCombo.AddItem("Install");
        _clientActionCombo.AddItem("Update");
        _clientActionCombo.AddItem("Play");
        _clientActionCombo.ItemSelected += OnClientActionSelected;
        
        // Setup Server Combo
        _serverActionCombo.AddItem("Install Server");
        _serverActionCombo.AddItem("Update Server");
        _serverActionCombo.ItemSelected += OnServerActionSelected;

        // Connections
        _clientBrowseBtn.Pressed += () => _clientFolderDialog.PopupCentered(new Vector2I(600, 400));
        _serverBrowseBtn.Pressed += () => _serverFolderDialog.PopupCentered(new Vector2I(600, 400));
        
        _clientFolderDialog.DirSelected += (dir) => _clientPathEdit.Text = dir;
        _serverFolderDialog.DirSelected += (dir) => _serverPathEdit.Text = dir;

        _clientActionBtn.Pressed += OnClientActionExecute;
        _serverActionBtn.Pressed += OnServerActionExecute;
        
        _passwordSubmitBtn.Pressed += OnPasswordSubmit;
        _passwordCancelBtn.Pressed += () => _passwordDialog.Hide();
        _serverCloseBtn.Pressed += () => _serverPanel.Hide();

        // Blog Connections
        _prevBtn.Pressed += () => { if (_currentBlogIndex > 0) { _currentBlogIndex--; UpdateBlogDisplay(); } };
        _nextBtn.Pressed += () => { if (_currentBlogIndex < _blogs.Count - 1) { _currentBlogIndex++; UpdateBlogDisplay(); } };
        _centerClickBtn.Pressed += ShowFullArticle;
        _fullBlogCloseBtn.Pressed += () => _fullBlogDialog.Hide();
        
        // Initial State
        _clientPathEdit.Text = @"C:\Games\EQMUD";
        _serverPathEdit.Text = @"C:\Servers\EQMUD_Server";
        
        _clientStatus.Text = "Idle.";
        _serverStatus.Text = "Idle.";
        _clientProgress.Value = 0;
        _serverProgress.Value = 0;
        
        UpdateClientActionBtnText(0);
        UpdateServerActionBtnText(0);
        
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

    private void OnClientActionSelected(long index) => UpdateClientActionBtnText((int)index);
    private void OnServerActionSelected(long index) => UpdateServerActionBtnText((int)index);

    private void UpdateClientActionBtnText(int index)
    {
        switch(index)
        {
            case 0: _clientActionBtn.Text = "INSTALL"; break;
            case 1: _clientActionBtn.Text = "UPDATE"; break;
            case 2: _clientActionBtn.Text = "PLAY"; break;
        }
    }

    private void UpdateServerActionBtnText(int index)
    {
        switch(index)
        {
            case 0: _serverActionBtn.Text = "INSTALL SERVER"; break;
            case 1: _serverActionBtn.Text = "UPDATE SERVER"; break;
        }
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
            _serverPanel.Show();
        }
        else
        {
            _passwordEdit.Text = "";
            GD.Print("Incorrect admin password.");
        }
    }

    private async void OnClientActionExecute()
    {
        int action = _clientActionCombo.Selected;
        string path = _clientPathEdit.Text;
        
        _clientActionBtn.Disabled = true;
        _clientActionCombo.Disabled = true;
        
        if (action == 0 || action == 1) // Install or Update
        {
            _clientStatus.Text = action == 0 ? $"Installing to {path}..." : $"Updating at {path}...";
            
            for (int i = 0; i <= 100; i += 5)
            {
                _clientProgress.Value = i;
                await ToSignal(GetTree().CreateTimer(0.05f), SceneTreeTimer.SignalName.Timeout);
            }
            
            _clientStatus.Text = action == 0 ? "Installation Complete." : "Update Complete.";
            
            // Switch to Play mode automatically
            _clientActionCombo.Selected = 2;
            UpdateClientActionBtnText(2);
        }
        else if (action == 2) // Play
        {
            _clientStatus.Text = "Launching game...";
            await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
            _clientStatus.Text = "Game is running.";
        }

        _clientActionBtn.Disabled = false;
        _clientActionCombo.Disabled = false;
    }

    private async void OnServerActionExecute()
    {
        int action = _serverActionCombo.Selected;
        string path = _serverPathEdit.Text;
        
        _serverActionBtn.Disabled = true;
        _serverActionCombo.Disabled = true;
        
        _serverStatus.Text = action == 0 ? $"Installing server to {path}..." : $"Updating server at {path}...";
        
        for (int i = 0; i <= 100; i += 5)
        {
            _serverProgress.Value = i;
            await ToSignal(GetTree().CreateTimer(0.05f), SceneTreeTimer.SignalName.Timeout);
        }
        
        _serverStatus.Text = action == 0 ? "Server Installation Complete." : "Server Update Complete.";
        
        _serverActionBtn.Disabled = false;
        _serverActionCombo.Disabled = false;
    }
}
