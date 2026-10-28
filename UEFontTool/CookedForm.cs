namespace UEFontTool;

public sealed class CookedForm : Form
{
    readonly DataGridView grid = new() { Dock=DockStyle.Fill,AllowUserToAddRows=false,AutoGenerateColumns=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=true,BackgroundColor=Color.FromArgb(18,18,18),AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells };
    readonly ComboBox version = new() { DropDownStyle=ComboBoxStyle.DropDownList,Width=100 };
    readonly TextBox name = new() { Text="zz_UEFontTool_P",Width=200 };
    readonly CheckBox allow = new() { Text="允许丢失私用字形 / Allow PUA loss",AutoSize=true };
    readonly TextBox details = new() { Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Both };
    readonly Label state = new() { AutoSize=true,Text="先扫描 / Scan first" };
    readonly string game; readonly string? key;
    string? analysis,mapping; bool busy;
    public CookedForm(string game,string? key)
    {
        this.game=game;this.key=key; Text="内联 FontFace / IoStore";Icon=AppBranding.CreateIcon();Width=1250;Height=850;MinimumSize=new Size(950,650);StartPosition=FormStartPosition.CenterParent;
        BackColor=Color.Black;ForeColor=Color.Silver;
        details.BackColor=Color.FromArgb(18,18,18);details.ForeColor=Color.Silver;
        grid.DefaultCellStyle.BackColor=Color.FromArgb(18,18,18);grid.DefaultCellStyle.ForeColor=Color.Silver;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(75,20,24);grid.DefaultCellStyle.SelectionForeColor=Color.White;
        grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.Black;grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.IndianRed;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,80));layout.RowStyles.Add(new RowStyle(SizeType.Percent,72));layout.RowStyles.Add(new RowStyle(SizeType.Percent,28));Controls.Add(layout);
        var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true};layout.Controls.Add(bar);
        version.Items.AddRange(new object[]{"UE4_26","UE4_27","UE5_0","UE5_1","UE5_2","UE5_3","UE5_4","UE5_5","UE5_6","UE5_7"});
        version.SelectedItem=game.Contains("RESONANCE",StringComparison.OrdinalIgnoreCase)?"UE5_6":game.Contains("Hogwarts",StringComparison.OrdinalIgnoreCase)?"UE4_27":"UE5_4";
        bar.Controls.Add(new Label{Text="引擎版本 / Engine",AutoSize=true});bar.Controls.Add(version);bar.Controls.Add(name);
        void Button(string text,Action click){var b=new TextButton{Text=text,AutoSize=true,BackColor=Color.Black,ForeColor=Color.IndianRed};b.Click+=(_,_)=>click();bar.Controls.Add(b);}
        Button("扫描 / Scan",()=>Run(false));Button("全选 / All",()=>Check(true));Button("清空 / None",()=>Check(false));
        Button("选择新字体 / Choose font",()=>{using var d=new OpenFileDialog{Filter="TTF / OTF|*.ttf;*.otf"};if(d.ShowDialog(this)==DialogResult.OK)foreach(DataGridViewRow r in grid.Rows)if(r.Cells[0].Value is true)r.Cells[3].Value=d.FileName;});
        Button("生成 / Build",()=>Run(true));bar.Controls.Add(allow);bar.Controls.Add(state);
        Button("映射 / .usmap",()=>{using var d=new OpenFileDialog{Filter="Mappings|*.usmap"};if(d.ShowDialog(this)==DialogResult.OK)mapping=d.FileName;});
        grid.Columns.Add(new DataGridViewCheckBoxColumn{HeaderText="选择",Width=50});
        grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="字体 / Font",Width=230,ReadOnly=true});
        grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="资源路径 / Resource",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,ReadOnly=true,MinimumWidth=250});
        grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="新字体 / Replacement",Width=260});
        grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;layout.Controls.Add(grid);layout.Controls.Add(details);
        grid.SelectionChanged+=(_,_)=>{if(grid.CurrentRow?.Tag is CookedReport r)details.Text=System.Text.Json.JsonSerializer.Serialize(r,FontService.Json);};
        FormClosing+=(_,e)=>{if(busy)e.Cancel=true;};
    }
    void Check(bool value){foreach(DataGridViewRow r in grid.Rows)if(r.Tag is CookedReport report && (report.Faces.Count>0||report.ExternalFaces.Count>0))r.Cells[0].Value=value;}
    async void Run(bool build)
    {
        if(busy)return;busy=true;grid.EndEdit();string v=version.Text,n=name.Text;bool pua=allow.Checked;string? old=Environment.GetEnvironmentVariable("UEFONTTOOL_AES_KEY"),oldMap=Environment.GetEnvironmentVariable("UEFONTTOOL_USMAP");
        try
        {
            Environment.SetEnvironmentVariable("UEFONTTOOL_AES_KEY",key);Environment.SetEnvironmentVariable("UEFONTTOOL_USMAP",mapping);state.Text="处理中 / Working…";
            string output=Path.Combine(FontService.DataRoot,"cooked",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);
            if(!build)
            {
                await Task.Run(()=>IoStoreFonts.Extract(game,output,v,"Font"));analysis=output;
                var reports=System.Text.Json.JsonSerializer.Deserialize<CookedReport[]>(File.ReadAllText(Path.Combine(output,"font-analysis.json")))!;
                grid.Rows.Clear();foreach(var r in reports){int i=grid.Rows.Add(false,Path.GetFileNameWithoutExtension(r.Path),Path.GetRelativePath(output,r.Path).Replace('\\','/'),"");grid.Rows[i].Tag=r;}
                details.Text=output;state.Text=$"{reports.Length} resources / {reports.Sum(r=>r.Faces.Count)} inline";
            }
            else
            {
                if(analysis==null)throw new InvalidOperationException("请先扫描 / Scan first.");
                var plan=new CookedPlan{Game=game,Version=v,OutputName=IoStoreFonts.Name(n),AllowPrivateUseLoss=pua};
                foreach(DataGridViewRow row in grid.Rows)if(row.Cells[0].Value is true)plan.Replacements[(string)row.Cells[2].Value]=Convert.ToString(row.Cells[3].Value)??"";
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);string planPath=output+"-plan.json";File.WriteAllText(planPath,System.Text.Json.JsonSerializer.Serialize(plan,FontService.Json));
                await Task.Run(()=>IoStoreFonts.Build(planPath,output));details.Text="已生成并回读校验 / Built and verified\r\n"+output+"\r\n请在游戏内验证 / Verify appearance in game.";state.Text="完成 / Done";
            }
        }
        catch(Exception ex){state.Text="未完成 / Failed";details.Text=ex.Message;}
        finally{Environment.SetEnvironmentVariable("UEFONTTOOL_AES_KEY",old);Environment.SetEnvironmentVariable("UEFONTTOOL_USMAP",oldMap);busy=false;}
    }
}
