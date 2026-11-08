using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace TwoZoneClock {
    internal sealed class RegionChoice {
        internal readonly string Id;
        private readonly string label, searchTerms;
        // Both languages remain searchable, independent of Windows and interface language.
        private static readonly Dictionary<string, string[]> Names = new Dictionary<string, string[]> {
            { "Singapore Standard Time", new[] { "新加坡", "Singapore", "马来西亚 吉隆坡 Malaysia Kuala Lumpur SGT" } },
            { "China Standard Time", new[] { "中国 · 北京", "China · Beijing", "上海 广州 深圳 重庆 香港 澳门 乌鲁木齐 Shanghai Guangzhou Shenzhen Chongqing Hong Kong Macau Urumqi CST" } },
            { "Taipei Standard Time", new[] { "台湾 · 台北", "Taiwan · Taipei", "高雄 台中 Kaohsiung Taichung" } },
            { "Tokyo Standard Time", new[] { "日本 · 东京", "Japan · Tokyo", "大阪 札幌 京都 Osaka Sapporo Kyoto JST" } },
            { "Korea Standard Time", new[] { "韩国 · 首尔", "South Korea · Seoul", "南韩 釜山 Busan KST" } },
            { "North Korea Standard Time", new[] { "朝鲜 · 平壤", "North Korea · Pyongyang", "" } },
            { "India Standard Time", new[] { "印度 · 新德里", "India · New Delhi", "孟买 加尔各答 班加罗尔 Mumbai Kolkata Calcutta Bangalore Bengaluru Chennai IST" } },
            { "Eastern Standard Time", new[] { "美国东部 · 纽约", "US Eastern · New York", "美国 美国东岸 华盛顿 波士顿 迈阿密 加拿大 多伦多 渥太华 蒙特利尔 USA America United States Washington Boston Miami Canada Toronto Ottawa Montreal EST EDT" } },
            { "Central Standard Time", new[] { "美国中部 · 芝加哥", "US Central · Chicago", "美国 达拉斯 休斯敦 加拿大 温尼伯 USA America United States Dallas Houston Canada Winnipeg CST CDT" } },
            { "Pacific Standard Time", new[] { "美国西部 · 洛杉矶", "US Pacific · Los Angeles", "美国 美国西岸 旧金山 西雅图 拉斯维加斯 加拿大 温哥华 USA America United States San Francisco Seattle Las Vegas Canada Vancouver PST PDT" } },
            { "Mountain Standard Time", new[] { "美国山地 · 丹佛", "US Mountain · Denver", "美国 盐湖城 加拿大 卡尔加里 埃德蒙顿 USA America United States Salt Lake City Canada Calgary Edmonton MST MDT" } },
            { "US Mountain Standard Time", new[] { "美国 · 凤凰城", "US · Phoenix", "美国 亚利桑那 USA America United States Arizona MST" } },
            { "Alaskan Standard Time", new[] { "美国 · 阿拉斯加", "US · Alaska", "美国 安克雷奇 USA America United States Anchorage AKST AKDT" } },
            { "Hawaiian Standard Time", new[] { "美国 · 夏威夷", "US · Hawaii", "美国 檀香山 火奴鲁鲁 USA America United States Honolulu HST" } },
            { "Atlantic Standard Time", new[] { "加拿大 · 哈利法克斯", "Canada · Halifax", "大西洋 新斯科舍 百慕大 Atlantic Nova Scotia Bermuda AST ADT" } },
            { "Newfoundland Standard Time", new[] { "加拿大 · 纽芬兰", "Canada · Newfoundland", "圣约翰斯 St Johns NST NDT" } },
            { "Canada Central Standard Time", new[] { "加拿大 · 萨斯喀彻温", "Canada · Saskatchewan", "里贾纳 Regina" } },
            { "US Eastern Standard Time", new[] { "美国 · 印第安纳", "US · Indiana", "美国 印第安纳波利斯 USA America United States Indianapolis" } },
            { "GMT Standard Time", new[] { "英国 · 伦敦", "UK · London", "英国 爱尔兰 都柏林 爱丁堡 葡萄牙 里斯本 United Kingdom Britain England Scotland Edinburgh Ireland Dublin Portugal Lisbon Belfast GMT BST" } },
            { "Greenwich Standard Time", new[] { "冰岛 · 雷克雅未克", "Iceland · Reykjavik", "加纳 阿克拉 塞内加尔 达喀尔 Ghana Accra Senegal Dakar GMT" } },
            { "Romance Standard Time", new[] { "法国 · 巴黎", "France · Paris", "西班牙 马德里 比利时 布鲁塞尔 丹麦 哥本哈根 Spain Madrid Belgium Brussels Denmark Copenhagen CET CEST" } },
            { "W. Europe Standard Time", new[] { "德国 · 柏林", "Germany · Berlin", "法兰克福 慕尼黑 意大利 罗马 荷兰 阿姆斯特丹 瑞士 苏黎世 瑞典 斯德哥尔摩 挪威 奥斯陆 奥地利 维也纳 Frankfurt Munich Italy Rome Netherlands Amsterdam Switzerland Zurich Sweden Stockholm Norway Oslo Austria Vienna CET CEST" } },
            { "Central Europe Standard Time", new[] { "中欧 · 布达佩斯", "Central Europe · Budapest", "匈牙利 捷克 布拉格 斯洛伐克 布拉迪斯拉发 塞尔维亚 贝尔格莱德 Hungary Czech Prague Slovakia Bratislava Serbia Belgrade" } },
            { "Central European Standard Time", new[] { "波兰 · 华沙", "Poland · Warsaw", "克罗地亚 萨格勒布 萨拉热窝 斯科普里 Croatia Zagreb Sarajevo Skopje" } },
            { "FLE Standard Time", new[] { "芬兰 · 赫尔辛基", "Finland · Helsinki", "乌克兰 基辅 保加利亚 索非亚 爱沙尼亚 塔林 拉脱维亚 里加 立陶宛 维尔纽斯 Ukraine Kyiv Kiev Bulgaria Sofia Estonia Tallinn Latvia Riga Lithuania Vilnius" } },
            { "GTB Standard Time", new[] { "希腊 · 雅典", "Greece · Athens", "罗马尼亚 布加勒斯特 塞浦路斯 尼科西亚 Romania Bucharest Cyprus Nicosia" } },
            { "Turkey Standard Time", new[] { "土耳其 · 伊斯坦布尔", "Turkey · Istanbul", "安卡拉 Türkiye Ankara" } },
            { "Russian Standard Time", new[] { "俄罗斯 · 莫斯科", "Russia · Moscow", "圣彼得堡 St Petersburg MSK" } },
            { "Belarus Standard Time", new[] { "白俄罗斯 · 明斯克", "Belarus · Minsk", "" } },
            { "Ekaterinburg Standard Time", new[] { "俄罗斯 · 叶卡捷琳堡", "Russia · Yekaterinburg", "Ekaterinburg" } },
            { "N. Central Asia Standard Time", new[] { "俄罗斯 · 新西伯利亚", "Russia · Novosibirsk", "" } },
            { "North Asia Standard Time", new[] { "俄罗斯 · 克拉斯诺亚尔斯克", "Russia · Krasnoyarsk", "" } },
            { "North Asia East Standard Time", new[] { "俄罗斯 · 伊尔库茨克", "Russia · Irkutsk", "" } },
            { "Vladivostok Standard Time", new[] { "俄罗斯 · 海参崴", "Russia · Vladivostok", "符拉迪沃斯托克" } },
            { "AUS Eastern Standard Time", new[] { "澳大利亚 · 悉尼", "Australia · Sydney", "澳洲 墨尔本 堪培拉 Melbourne Canberra AEST AEDT" } },
            { "E. Australia Standard Time", new[] { "澳大利亚 · 布里斯班", "Australia · Brisbane", "澳洲 昆士兰 Queensland" } },
            { "W. Australia Standard Time", new[] { "澳大利亚 · 珀斯", "Australia · Perth", "澳洲 西澳 Western Australia" } },
            { "Cen. Australia Standard Time", new[] { "澳大利亚 · 阿德莱德", "Australia · Adelaide", "澳洲 南澳 South Australia" } },
            { "AUS Central Standard Time", new[] { "澳大利亚 · 达尔文", "Australia · Darwin", "澳洲 北领地 Northern Territory" } },
            { "Tasmania Standard Time", new[] { "澳大利亚 · 霍巴特", "Australia · Hobart", "澳洲 塔斯马尼亚 Tasmania" } },
            { "New Zealand Standard Time", new[] { "新西兰 · 奥克兰", "New Zealand · Auckland", "惠灵顿 基督城 Wellington Christchurch NZST NZDT" } },
            { "Arabian Standard Time", new[] { "阿联酋 · 迪拜", "UAE · Dubai", "阿布扎比 阿曼 马斯喀特 United Arab Emirates Abu Dhabi Oman Muscat" } },
            { "Arab Standard Time", new[] { "沙特阿拉伯 · 利雅得", "Saudi Arabia · Riyadh", "科威特 卡塔尔 多哈 巴林 Kuwait Qatar Doha Bahrain" } },
            { "Arabic Standard Time", new[] { "伊拉克 · 巴格达", "Iraq · Baghdad", "" } },
            { "Iran Standard Time", new[] { "伊朗 · 德黑兰", "Iran · Tehran", "" } },
            { "Israel Standard Time", new[] { "以色列 · 耶路撒冷", "Israel · Jerusalem", "特拉维夫 Tel Aviv" } },
            { "Jordan Standard Time", new[] { "约旦 · 安曼", "Jordan · Amman", "" } },
            { "Middle East Standard Time", new[] { "黎巴嫩 · 贝鲁特", "Lebanon · Beirut", "" } },
            { "SE Asia Standard Time", new[] { "泰国 · 曼谷", "Thailand · Bangkok", "越南 河内 胡志明 印度尼西亚 雅加达 柬埔寨 金边 老挝 万象 Vietnam Hanoi Ho Chi Minh Indonesia Jakarta Cambodia Phnom Penh Laos Vientiane" } },
            { "W. Central Africa Standard Time", new[] { "尼日利亚 · 拉各斯", "Nigeria · Lagos", "中西非 阿布贾 喀麦隆 杜阿拉 刚果 金沙萨 West Central Africa Abuja Cameroon Douala Congo Kinshasa" } },
            { "South Africa Standard Time", new[] { "南非 · 约翰内斯堡", "South Africa · Johannesburg", "开普敦 比勒陀利亚 津巴布韦 哈拉雷 Cape Town Pretoria Zimbabwe Harare" } },
            { "E. Africa Standard Time", new[] { "肯尼亚 · 内罗毕", "Kenya · Nairobi", "东非 埃塞俄比亚 亚的斯亚贝巴 坦桑尼亚 达累斯萨拉姆 East Africa Ethiopia Addis Ababa Tanzania Dar es Salaam" } },
            { "Egypt Standard Time", new[] { "埃及 · 开罗", "Egypt · Cairo", "" } },
            { "Morocco Standard Time", new[] { "摩洛哥 · 卡萨布兰卡", "Morocco · Casablanca", "拉巴特 Rabat" } },
            { "Bangladesh Standard Time", new[] { "孟加拉国 · 达卡", "Bangladesh · Dhaka", "" } },
            { "Pakistan Standard Time", new[] { "巴基斯坦 · 卡拉奇", "Pakistan · Karachi", "伊斯兰堡 Islamabad" } },
            { "Sri Lanka Standard Time", new[] { "斯里兰卡 · 科伦坡", "Sri Lanka · Colombo", "" } },
            { "Nepal Standard Time", new[] { "尼泊尔 · 加德满都", "Nepal · Kathmandu", "Katmandu" } },
            { "Myanmar Standard Time", new[] { "缅甸 · 仰光", "Myanmar · Yangon", "Rangoon Burma" } },
            { "Afghanistan Standard Time", new[] { "阿富汗 · 喀布尔", "Afghanistan · Kabul", "" } },
            { "West Asia Standard Time", new[] { "乌兹别克斯坦 · 塔什干", "Uzbekistan · Tashkent", "土库曼斯坦 阿什哈巴德 Turkmenistan Ashgabat" } },
            { "Azerbaijan Standard Time", new[] { "阿塞拜疆 · 巴库", "Azerbaijan · Baku", "" } },
            { "Georgian Standard Time", new[] { "格鲁吉亚 · 第比利斯", "Georgia · Tbilisi", "" } },
            { "Caucasus Standard Time", new[] { "亚美尼亚 · 埃里温", "Armenia · Yerevan", "" } },
            { "Ulaanbaatar Standard Time", new[] { "蒙古 · 乌兰巴托", "Mongolia · Ulaanbaatar", "Ulan Bator" } },
            { "Argentina Standard Time", new[] { "阿根廷 · 布宜诺斯艾利斯", "Argentina · Buenos Aires", "" } },
            { "E. South America Standard Time", new[] { "巴西 · 圣保罗", "Brazil · Sao Paulo", "巴西利亚 里约热内卢 Brasília Brasilia São Paulo Rio de Janeiro" } },
            { "SA Pacific Standard Time", new[] { "哥伦比亚 · 波哥大", "Colombia · Bogota", "秘鲁 利马 厄瓜多尔 基多 巴拿马 Peru Lima Ecuador Quito Panama Bogotá" } },
            { "SA Western Standard Time", new[] { "玻利维亚 · 拉巴斯", "Bolivia · La Paz", "波多黎各 圣胡安 多米尼加 圣多明各 Puerto Rico San Juan Dominican Republic Santo Domingo" } },
            { "Pacific SA Standard Time", new[] { "智利 · 圣地亚哥", "Chile · Santiago", "" } },
            { "Venezuela Standard Time", new[] { "委内瑞拉 · 加拉加斯", "Venezuela · Caracas", "" } },
            { "Montevideo Standard Time", new[] { "乌拉圭 · 蒙得维的亚", "Uruguay · Montevideo", "" } },
            { "Paraguay Standard Time", new[] { "巴拉圭 · 亚松森", "Paraguay · Asuncion", "Asunción" } },
            { "Central Standard Time (Mexico)", new[] { "墨西哥 · 墨西哥城", "Mexico · Mexico City", "瓜达拉哈拉 蒙特雷 Guadalajara Monterrey" } },
            { "Pacific Standard Time (Mexico)", new[] { "墨西哥 · 蒂华纳", "Mexico · Tijuana", "下加利福尼亚 Baja California" } },
            { "Eastern Standard Time (Mexico)", new[] { "墨西哥 · 坎昆", "Mexico · Cancun", "Cancún" } },
            { "Central America Standard Time", new[] { "中美洲 · 圣何塞", "Central America · San Jose", "哥斯达黎加 危地马拉 萨尔瓦多 洪都拉斯 尼加拉瓜 Costa Rica Guatemala El Salvador Honduras Nicaragua" } },
            { "Cuba Standard Time", new[] { "古巴 · 哈瓦那", "Cuba · Havana", "" } },
            { "Fiji Standard Time", new[] { "斐济 · 苏瓦", "Fiji · Suva", "" } },
            { "Samoa Standard Time", new[] { "萨摩亚 · 阿皮亚", "Samoa · Apia", "" } },
            { "Tonga Standard Time", new[] { "汤加 · 努库阿洛法", "Tonga · Nukualofa", "Nuku'alofa" } },
            { "West Pacific Standard Time", new[] { "西太平洋 · 关岛", "West Pacific · Guam", "巴布亚新几内亚 莫尔兹比港 Papua New Guinea Port Moresby" } },
            { "Central Pacific Standard Time", new[] { "中太平洋 · 所罗门群岛", "Central Pacific · Solomon Islands", "新喀里多尼亚 努美阿 New Caledonia Noumea" } },
            { "UTC", new[] { "协调世界时 · UTC", "Coordinated Universal Time · UTC", "世界标准时间 世界时 格林威治 universal zero GMT" } }
        };
        private static readonly string[] Common = {
            "Singapore Standard Time", "China Standard Time", "Taipei Standard Time",
            "Eastern Standard Time", "Central Standard Time", "Pacific Standard Time", "Mountain Standard Time",
            "GMT Standard Time", "Romance Standard Time", "Tokyo Standard Time", "Korea Standard Time",
            "India Standard Time", "AUS Eastern Standard Time", "New Zealand Standard Time", "UTC"
        };
        private RegionChoice(string id, string display, string terms) { Id = id; label = display; searchTerms = terms; }
        public override string ToString() { return label; }
        internal bool Matches(string query) {
            if (String.IsNullOrWhiteSpace(query)) return true;
            foreach (string word in query.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                if (CultureInfo.InvariantCulture.CompareInfo.IndexOf(searchTerms, word,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) < 0) return false;
            return true;
        }
        internal static string ShortName(string id) { return ShortName(id, false); }
        internal static string ShortName(string id, bool english) {
            if (id == "local") return english ? "Computer local" : "本机地区";
            string[] names;
            if (Names.TryGetValue(id, out names)) {
                if (id == "Eastern Standard Time") return english ? "US Eastern" : "美国东部";
                if (id == "Central Standard Time") return english ? "US Central" : "美国中部";
                if (id == "Pacific Standard Time") return english ? "US Pacific" : "美国西部";
                if (id == "Mountain Standard Time") return english ? "US Mountain" : "美国山地";
                string name = names[english ? 1 : 0];
                int split = name.LastIndexOf(" · ", StringComparison.Ordinal);
                return split < 0 ? name : name.Substring(split + 3);
            }
            var zone = ClockTime.ResolveZone(id);
            return english ? zone.Id.Replace(" Standard Time", "") : zone.StandardName;
        }
        private static string ZoneName(TimeZoneInfo zone, bool english) {
            string[] names;
            if (Names.TryGetValue(zone.Id, out names)) return names[english ? 1 : 0];
            // Never leak Chinese Windows display names into the English interface.
            return english ? zone.Id.Replace(" Standard Time", "") : zone.StandardName;
        }
        private static RegionChoice FromZone(TimeZoneInfo zone, bool english, bool local) {
            string[] names;
            string aliases = Names.TryGetValue(zone.Id, out names) ? String.Join(" ", names) : "";
            string display = ZoneName(zone, english) + "  ·  " + ClockTime.OffsetLabel(zone.BaseUtcOffset);
            if (local) display = (english ? "Follow computer time zone — " : "跟随电脑时区 — ") + display;
            return new RegionChoice(local ? "local" : zone.Id, display,
                zone.Id + " " + zone.DisplayName + " " + zone.StandardName + " " + aliases + " " + display
                + (local ? " local computer system automatic auto 本机 本地 电脑 系统 自动 当前" : ""));
        }
        internal static RegionChoice[] All() { return All(false); }
        internal static RegionChoice[] All(bool english) {
            var result = new List<RegionChoice>();
            var used = new HashSet<string>();
            result.Add(FromZone(TimeZoneInfo.Local, english, true));
            foreach (string id in Common) {
                if (!ClockTime.IsValidZone(id)) continue;
                result.Add(FromZone(ClockTime.ResolveZone(id), english, false)); used.Add(id);
            }
            foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
                if (used.Add(zone.Id)) result.Add(FromZone(zone, english, false));
            return result.ToArray();
        }
    }

    internal sealed class RegionSettingsForm : Form {
        private readonly SearchableRegionCombo source, target;
        private readonly ComboBox language;
        private readonly Label heading, explanation, sourceLabel, targetLabel;
        private readonly Label sourceStatus, targetStatus, note;
        private readonly Button swap, save, cancel;
        private readonly System.Windows.Forms.Timer timer;
        private readonly Icon themedIcon;
        private readonly bool use24;
        private float themeScale;
        private bool renderingPreview, updating, english;
        private string selectedSourceId, selectedTargetId;
        private RegionChoice[] choices;
        protected override bool ShowWithoutActivation { get { return renderingPreview; } }
        internal string SourceTimeZoneId { get { return selectedSourceId; } }
        internal string TargetTimeZoneId { get { return selectedTargetId; } }
        internal bool English { get { return english; } }

        internal RegionSettingsForm(string sourceId, string targetId, bool twentyFourHours)
            : this(sourceId, targetId, twentyFourHours, false) { }
        internal RegionSettingsForm(string sourceId, string targetId, bool twentyFourHours, bool useEnglish) {
            use24 = twentyFourHours; english = useEnglish;
            selectedSourceId = ClockTime.IsValidZone(sourceId) ? sourceId : "local";
            selectedTargetId = ClockTime.IsValidZone(targetId) ? targetId : "local";
            // Scale the fixed layout and painted artwork together; point fonts use the same screen DPI.
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(432, 330); FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = WhaleTheme.Surface; ForeColor = WhaleTheme.Ink;
            Font = new Font("Microsoft YaHei UI", 9f);
            themedIcon = WhaleTheme.CreateIcon(); Icon = themedIcon;
            DoubleBuffered = true;

            heading = AddLabel("", 80, 13, 224, 30, 14f, ForeColor);
            AddLabel("中文 / English", 306, 8, 110, 19, 8.5f, WhaleTheme.Muted);
            language = new ComboBox { Name = "languageSelector", Location = new Point(310, 29), Size = new Size(106, 28), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.White, ForeColor = WhaleTheme.Ink, FlatStyle = FlatStyle.Flat };
            Controls.Add(language);
            language.Items.Add("中文"); language.Items.Add("English");
            language.SelectedIndex = english ? 1 : 0;
            language.AccessibleName = "中文 / English";
            explanation = AddLabel("", 80, 47, 224, 24, 8.5f, WhaleTheme.Muted);
            sourceLabel = AddLabel("", 16, 80, 400, 22, 10f, Color.FromArgb(146, 109, 48));
            source = CreateSelector("sourceSelector", 16, 104, 400);
            sourceStatus = AddLabel("", 16, 139, 400, 24, 8.5f, WhaleTheme.Muted);
            targetLabel = AddLabel("", 16, 173, 400, 22, 10f, WhaleTheme.Blue);
            target = CreateSelector("targetSelector", 16, 197, 400);
            targetStatus = AddLabel("", 16, 232, 400, 24, 8.5f, WhaleTheme.Muted);
            note = AddLabel("", 16, 260, 400, 20, 8.5f, WhaleTheme.Muted);

            swap = CreateButton("swapButton", 16, 286, 132);
            save = CreateButton("saveButton", 248, 286, 78, true);
            cancel = CreateButton("cancelButton", 338, 286, 78); cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = save; CancelButton = cancel;

            source.SelectionStateChanged += delegate {
                if (updating) return;
                if (source.HasSelection) selectedSourceId = source.SelectedRegionId;
                RefreshState();
            };
            target.SelectionStateChanged += delegate {
                if (updating) return;
                if (target.HasSelection) selectedTargetId = target.SelectedRegionId;
                RefreshState();
            };
            language.SelectedIndexChanged += delegate {
                if (updating) return;
                english = language.SelectedIndex == 1; ApplyLanguage();
            };
            swap.Click += delegate {
                string first = selectedSourceId; selectedSourceId = selectedTargetId; selectedTargetId = first;
                updating = true;
                source.SetChoices(choices, selectedSourceId, false); target.SetChoices(choices, selectedTargetId, false);
                updating = false; RefreshState();
            };
            save.Click += delegate {
                if (source.HasSelection && target.HasSelection) { DialogResult = DialogResult.OK; Close(); }
            };
            timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += delegate { RefreshTimes(); };
            Shown += delegate { RefreshTimes(); timer.Start(); FocusSave(); };
            ApplyLanguage();
            source.TabIndex = 0; target.TabIndex = 1; language.TabIndex = 2;
            swap.TabIndex = 3; save.TabIndex = 4; cancel.TabIndex = 5;
            using (var graphics = CreateGraphics()) themeScale = graphics.DpiX/96f;
            if (Math.Abs(themeScale-1f) > 0.01f) Scale(new SizeF(themeScale, themeScale));
        }
        private void ApplyLanguage() {
            Text = english ? "Whalegirl Clock · Regions" : "鲸娘两地时钟 · 地区设置";
            heading.Text = english ? "Whalegirl Clock" : "鲸娘陪你看时间";
            explanation.Text = english ? "Type a city in either region box." : "在框内输入地名，选择匹配结果。";
            sourceLabel.Text = english ? "Source region" : "原本地区";
            targetLabel.Text = english ? "Target region" : "目标地区";
            source.AccessibleName = sourceLabel.Text; target.AccessibleName = targetLabel.Text;
            note.Text = english ? "Search in 中文 / English · Auto DST" : "支持中文 / English 搜索 · 自动调整夏令时";
            swap.Text = english ? "Swap regions" : "交换两个地区";
            save.Text = english ? "Save" : "保存"; cancel.Text = english ? "Cancel" : "取消";
            choices = RegionChoice.All(english);
            updating = true;
            source.SetChoices(choices, selectedSourceId, true);
            target.SetChoices(choices, selectedTargetId, true);
            updating = false; RefreshState();
        }
        private Label AddLabel(string text, int x, int y, int width, int height, float size, Color color) {
            var label = new Label { Text = text, AutoSize = false, Location = new Point(x, y), Size = new Size(width, height),
                Font = new Font("Microsoft YaHei UI", size), ForeColor = color, BackColor = Color.Transparent, AutoEllipsis = true };
            Controls.Add(label); return label;
        }
        private SearchableRegionCombo CreateSelector(string name, int x, int y, int width) {
            var selector = new SearchableRegionCombo { Name = name, Location = new Point(x, y), Size = new Size(width, 28),
                BackColor = Color.White, ForeColor = ForeColor, FlatStyle = FlatStyle.Flat, DropDownWidth = width };
            Controls.Add(selector); return selector;
        }
        private Button CreateButton(string name, int x, int y, int width, bool primary = false) {
            var button = new CandyButton { Name = name, Location = new Point(x, y), Size = new Size(width, 30),
                BackColor = Color.White, ForeColor = ForeColor, FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false };
            WhaleTheme.StyleButton(button, primary);
            Controls.Add(button); return button;
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            var state = e.Graphics.Save();
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.ScaleTransform(themeScale, themeScale);
            WhaleTheme.DrawMascot(e.Graphics, new RectangleF(10, 4, 66, 66));
            WhaleTheme.DrawCard(e.Graphics, new RectangleF(8, 76, 415, 90), WhaleTheme.Day, 12);
            WhaleTheme.DrawCard(e.Graphics, new RectangleF(8, 169, 415, 90), WhaleTheme.Night, 12);
            e.Graphics.Restore(state);
        }
        private void RefreshState() {
            UpdateStatus(source, sourceStatus);
            UpdateStatus(target, targetStatus);
            save.Enabled = source.HasSelection && target.HasSelection;
            RefreshTimes();
        }
        private void UpdateStatus(SearchableRegionCombo box, Label status) {
            bool pending = !box.HasSelection;
            status.ForeColor = pending ? Color.FromArgb(156, 102, 43) : WhaleTheme.Muted;
            if (box.Items.Count == 0) {
                status.Text = english ? "No matches. Try another name, or Esc to restore." : "没有匹配地区，换个地名试试，或按 Esc 恢复。";
            } else if (pending) {
                status.Text = english ? box.Items.Count + " matches — choose from the dropdown." : "找到 " + box.Items.Count + " 个地区，请点选下拉结果。";
            }
        }
        private void RefreshTimes() {
            var instant = DateTimeOffset.UtcNow;
            if (source.HasSelection) sourceStatus.Text = Describe(instant, selectedSourceId);
            if (target.HasSelection) targetStatus.Text = Describe(instant, selectedTargetId);
        }
        private string Describe(DateTimeOffset instant, string id) {
            var reading = ClockTime.Read(instant, id, use24, false);
            var zone = ClockTime.ResolveZone(id);
            return reading.Time
                + " · " + reading.Local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                + " · " + ClockTime.OffsetLabel(reading.Local.Offset)
                + (zone.IsDaylightSavingTime(instant) ? (english ? " · DST" : " · 夏令时") : "");
        }
        internal void RenderPreview(string path) { RenderSearchPreview(path, ""); }
        internal void RenderPreview(string path, float previewScale) { RenderSearchPreview(path, "", previewScale); }
        internal void RenderSearchPreview(string path, string query) {
            RenderSearchPreview(path, query, themeScale);
        }
        private void FocusSave() {
            ActiveControl = save;
            source.SelectionStart = source.Text.Length; source.SelectionLength = 0;
            target.SelectionStart = target.Text.Length; target.SelectionLength = 0;
        }
        internal void RenderSearchPreview(string path, string query, float previewScale) {
            if (!String.IsNullOrEmpty(query)) source.Text = query;
            renderingPreview = true;
            if (previewScale < 0.5f || previewScale > 3f) throw new ArgumentOutOfRangeException("previewScale");
            float? previousScale = WhaleTheme.RenderScaleOverride; WhaleTheme.RenderScaleOverride = previewScale;
            try {
            float ratio = previewScale / themeScale;
            if (Math.Abs(ratio-1f) > 0.001f) Scale(new SizeF(ratio, ratio));
            themeScale = previewScale;
            ClientSize = new Size((int)Math.Round(432*themeScale), (int)Math.Round(330*themeScale));
            NormalizePreviewFonts(this, previewScale);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-30000, -30000);
            Show(); Application.DoEvents();
            FocusSave();
            using (var bitmap = new Bitmap(Width, Height)) {
                DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            Close();
            } finally { WhaleTheme.RenderScaleOverride = previousScale; }
        }
        internal static void NormalizePreviewFonts(Control root, float scale) {
            var fonts = new Dictionary<Control, Font>();
            CollectPreviewFonts(root, fonts);
            foreach (var item in fonts) item.Key.Font = new Font(item.Value.FontFamily, item.Value.SizeInPoints*96f/72f*scale, item.Value.Style, GraphicsUnit.Pixel);
        }
        private static void CollectPreviewFonts(Control root, Dictionary<Control, Font> fonts) {
            fonts[root] = root.Font;
            foreach (Control child in root.Controls) CollectPreviewFonts(child, fonts);
        }
        protected override void Dispose(bool disposing) {
            if (disposing) { if (timer != null) timer.Dispose(); if (themedIcon != null) themedIcon.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
