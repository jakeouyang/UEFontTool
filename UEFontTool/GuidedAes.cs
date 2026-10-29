namespace UEFontTool;

public sealed partial class GuidedForm
{
    readonly Button aesEntry=Button();
    bool encryptedArchives;
    AesPrompt aesPrompt;

    string AesMessage()=>aesPrompt switch
    {
        AesPrompt.Missing=>T("检测到加密资源，需要当前游戏版本的 AES 密钥。点击右侧“输入 AES 密钥”，粘贴后重新检测；当前字体列表可能不完整。","Encrypted resources require an AES key for this game build. Click Enter AES key, paste it and rescan; the current font list may be incomplete."),
        AesPrompt.InvalidFormat=>T("AES 密钥格式不正确。点击右侧入口，输入 64 位十六进制（可带 0x）或 32 字节 Base64 密钥，再重新检测。","Invalid AES key format. Use the button to enter 64 hex digits (optional 0x) or a Base64-encoded 32-byte key, then rescan."),
        _=>T("加密资源尚未成功读取。请点击右侧“检查 AES 密钥”，核对密钥是否匹配当前游戏版本；引擎版本或资源格式不兼容也可能导致失败。","Encrypted resources could not be read. Click Check AES key and verify it matches this game build; an unsupported engine version or archive format may also cause this failure.")
    };
    void RefreshAesEntry()
    {
        aesEntry.Visible=encryptedArchives||aesPrompt!=AesPrompt.None;
        aesEntry.Text=aesPrompt==AesPrompt.CheckKeyOrFormat?T("检查 AES 密钥…","Check AES key…"):string.IsNullOrWhiteSpace(key.Text)?T("输入 AES 密钥…","Enter AES key…"):T("修改 AES 密钥…","Change AES key…");
        aesEntry.ForeColor=aesPrompt==AesPrompt.None?Color.Silver:Color.Khaki;
    }
    async Task EditAesAsync()
    {
        if(busy)return;bool saved;
        using(var dialog=new DarkWindow{Text=T("AES 解密密钥","AES decryption key"),Width=760,Height=390,MinimumSize=new Size(620,350),StartPosition=FormStartPosition.CenterParent,Font=Font,MaximizeBox=false,MinimizeBox=false})
        {
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(20),ColumnCount=1,RowCount=4};table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));for(int i=0;i<4;i++)table.RowStyles.Add(new RowStyle(SizeType.AutoSize));dialog.Controls.Add(table);
        var note=new Label{AutoSize=true,Text=T("输入匹配当前游戏版本的 AES 密钥：64 位十六进制（可带 0x），或 32 字节 Base64。密钥仅保留在本次会话，不保存到设置或日志。工具不自动查找密钥。","Enter the AES key for this game build: 64 hex digits (optional 0x), or a Base64-encoded 32-byte key. Keys stay in this session and are not saved to settings or logs. The tool does not find keys automatically."),Margin=new Padding(0,0,0,12)};
        table.SizeChanged+=(_,_)=>note.MaximumSize=new Size(Math.Max(200,table.ClientSize.Width-table.Padding.Horizontal-20),0);table.Controls.Add(note,0,0);
        var input=Input();input.UseSystemPasswordChar=true;input.FieldName=T("AES 密钥","AES key");input.PlaceholderText="0x…";input.Text=key.Text;input.Dock=DockStyle.Fill;table.Controls.Add(input,0,1);
        var show=new CheckBox{AutoSize=true,Text=T("显示密钥","Show key"),Margin=new Padding(0,12,0,12)};show.CheckedChanged+=(_,_)=>input.UseSystemPasswordChar=!show.Checked;table.Controls.Add(show,0,2);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};var apply=Button();apply.Text=T("应用并重新检测","Apply and rescan");var cancel=Button();cancel.Text=T("取消","Cancel");cancel.DialogResult=DialogResult.Cancel;actions.Controls.AddRange(new Control[]{apply,cancel});table.Controls.Add(actions,0,3);
        apply.Click+=(_,_)=>{try{if(PakTools.Keys(input.Text.Trim()).Count==0)throw new InvalidDataException(T("请输入 AES 密钥。","Enter an AES key."));key.Text=input.Text.Trim();dialog.DialogResult=DialogResult.OK;}catch(InvalidDataException ex){MessageBox.Show(dialog,ex.Message,"AES",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
        dialog.AcceptButton=apply;dialog.CancelButton=cancel;dialog.SetCaptionLanguage(zh);dialog.Shown+=(_,_)=>input.Focus();saved=dialog.ShowDialog(this)==DialogResult.OK;input.Clear();
        }
        if(saved)await AnalyzeAsync();
    }
    internal void CheckAesGuidance()
    {
        SelectLanguage(true);
        aesPrompt=AesPrompt.Missing;encryptedArchives=true;RefreshSummary();
        if(!aesEntry.Visible||!summary.Text.Contains("AES")||!aesEntry.Text.Contains("AES"))throw new Exception("Missing keys must have a visible prompt and direct entry.");
        SelectLanguage(false);if(!summary.Text.Contains("Encrypted resources require")||!aesEntry.Text.Contains("Enter AES key"))throw new Exception("AES guidance must switch languages.");
        aesPrompt=AesPrompt.CheckKeyOrFormat;RefreshSummary();if(!summary.Text.Contains("archive format"))throw new Exception("Encrypted read failures must retain format/version alternatives.");
        ReportFailure(new PrivateUseGlyphLossException("Game/Font/Test.uasset",new[]{0xe001}),"SYNTHETIC_GAME","build");if(aesPrompt!=AesPrompt.None||!summary.Text.Contains("custom characters"))throw new Exception("Glyph loss in an encrypted game must not be reported as an AES failure.");
        aesPrompt=AesPrompt.None;encryptedArchives=false;SelectLanguage(true);
    }
    internal bool OpensInEnglish=>!zh&&analyze.Text=="Detect fonts"&&languageMenu.Text=="中文";
    internal void RenderAesDialog(string file)
    {
        using var timer=new System.Windows.Forms.Timer{Interval=30};
        timer.Tick+=(_,_)=>{var dialog=OwnedForms.SingleOrDefault(f=>f.Visible&&f.IsHandleCreated);if(dialog==null)return;timer.Stop();using var bitmap=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,dialog.Size));bitmap.Save(file);dialog.DialogResult=DialogResult.Cancel;};
        timer.Start();EditAesAsync().GetAwaiter().GetResult();
    }
    internal void PreviewAesHint(){encryptedArchives=true;aesPrompt=AesPrompt.Missing;RefreshSummary();}
    internal void CheckAesEntryRetry(string root,string testKey)
    {
        SelectLanguage(false);game.Text=root;key.Clear();PreviewAesHint();bool opened=false,masked=false;
        using var timer=new System.Windows.Forms.Timer{Interval=25};
        timer.Tick+=(_,_)=>{
            var dialog=OwnedForms.SingleOrDefault(f=>f.Text=="AES decryption key");if(dialog==null||!dialog.Visible||!dialog.IsHandleCreated)return;
            timer.Stop();opened=true;var table=dialog.Controls.OfType<TableLayoutPanel>().Single();var input=table.Controls.OfType<FontInput>().Single();masked=input.UseSystemPasswordChar;input.Text=testKey;
            ((Button)dialog.AcceptButton!).PerformClick();
        };
        timer.Start();var operation=EditAesAsync();
        var wait=System.Diagnostics.Stopwatch.StartNew();while(!operation.IsCompleted&&wait.Elapsed<TimeSpan.FromSeconds(15)){Application.DoEvents();Thread.Sleep(10);}timer.Stop();
        if(!operation.IsCompleted)throw new Exception("AES rescan did not finish.");operation.GetAwaiter().GetResult();
        if(!opened||!masked||busy||!hasAnalyzed||aesPrompt!=AesPrompt.None||scan?.Fonts.Count!=1||scan.Errors.Count!=0||File.ReadAllText(Path.Combine(FontService.DataRoot,"settings.json")).Contains(testKey,StringComparison.OrdinalIgnoreCase))throw new Exception("AES entry must mask the key, apply it, rescan successfully and keep it out of settings.");
        Application.DoEvents();
    }
}
