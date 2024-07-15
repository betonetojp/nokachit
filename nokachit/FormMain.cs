using NNostr.Client;
using NNostr.Client.Protocols;
using SSTPLib;
using System.Diagnostics;

namespace nokachit
{
    public partial class FormMain : Form
    {
        #region �t�B�[���h
        private readonly NostrAccess _nostrAccess = new();

        private readonly string _configPath = Path.Combine(Application.StartupPath, "noka.config");

        private readonly FormSetting _formSetting = new();
        private FormManiacs _formManiacs = new();
        private FormRelayList _formRelayList = new();

        private string _npub = string.Empty;
        private string _npubHex = string.Empty;

        /// <summary>
        /// �t�H���C�[���J���̃n�b�V���Z�b�g
        /// </summary>
        private readonly HashSet<string> _followeesHexs = [];
        /// <summary>
        /// ���[�U�[����
        /// </summary>
        internal Dictionary<string, User?> Users = [];
        /// <summary>
        /// �L�[���[�h�ʒm
        /// </summary>
        internal KeywordNotifier Notifier = new();

        private int _cutLength;
        private int _cutNameLength;
        private bool _showOnlyFollowees;

        private double _tempOpacity = 1.00;

        private readonly DSSTPSender _ds = new("SakuraUnicode");
        private readonly string _SSTPMethod = "NOTIFY SSTP/1.1";
        private readonly Dictionary<string, string> _baseSSTPHeader = new(){
            {"Charset","UTF-8"},
            {"SecurityLevel","local"},
            {"Sender","noka chit-chat"},
            {"Option","nobreak,notranslate"},
            {"Event","OnNostr"},
            {"Reference0","Nostr/0.4"}
        };

        private string _ghostName = string.Empty;
        private bool _soleGhostsOnly = false;
        // �d���C�x���gID��ۑ����郊�X�g
        private readonly LinkedList<string> _displayedEventIds = new();
        private List<SoleGhost> _soleGhosts = [new SoleGhost(), new SoleGhost()];
        #endregion

        #region �R���X�g���N�^
        // �R���X�g���N�^
        public FormMain()
        {
            InitializeComponent();

            // �{�^���̉摜��DPI�ɍ��킹�ĕ\��
            float scale = CreateGraphics().DpiX / 96f;
            int size = (int)(16 * scale);
            if (scale < 2.0f)
            {
                buttonRelayList.Image = new Bitmap(Properties.Resources.icons8_list_16, size, size);
                buttonStop.Image = new Bitmap(Properties.Resources.icons8_stop_16, size, size);
                buttonStart.Image = new Bitmap(Properties.Resources.icons8_start_16, size, size);
                buttonStop.Image = new Bitmap(Properties.Resources.icons8_stop_16, size, size);
                buttonSetting.Image = new Bitmap(Properties.Resources.icons8_setting_16, size, size);
            }
            else
            {
                buttonRelayList.Image = new Bitmap(Properties.Resources.icons8_list_32, size, size);
                buttonStart.Image = new Bitmap(Properties.Resources.icons8_start_32, size, size);
                buttonStop.Image = new Bitmap(Properties.Resources.icons8_stop_32, size, size);
                buttonSetting.Image = new Bitmap(Properties.Resources.icons8_setting_32, size, size);
            }

            Setting.Load(_configPath);
            Users = Tools.LoadUsers();

            Location = Setting.Location;
            if (new Point(0, 0) == Location)
            {
                StartPosition = FormStartPosition.CenterScreen;
            }
            Size = Setting.Size;
            TopMost = Setting.TopMost;
            _cutLength = Setting.CutLength;
            _cutNameLength = Setting.CutNameLength;
            Opacity = Setting.Opacity;
            if (0 == Opacity)
            {
                Opacity = 1;
            }
            _showOnlyFollowees = Setting.ShowOnlyFollowees;
            _ghostName = Setting.Ghost;
            _soleGhostsOnly = Setting.SoleGhostsOnly;
            _npub = Setting.Npub;
            try
            {
                _npubHex = _npub.ConvertToHex();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }

            _formManiacs.MainForm = this;
        }
        #endregion

        #region Start�{�^��
        // Start�{�^��
        private async void ButtonStart_Click(object sender, EventArgs e)
        {
            try
            {
                int connectCount;
                if (null != _nostrAccess.Clients)
                {
                    connectCount = await _nostrAccess.ConnectAsync();
                }
                else
                {
                    connectCount = await _nostrAccess.ConnectAsync();
                    switch (connectCount)
                    {
                        case 0:
                            labelRelays.Text = "0 relays";
                            toolTipRelays.SetToolTip(labelRelays, string.Empty);
                            break;
                        case 1:
                            labelRelays.Text = _nostrAccess.Relays[0].ToString();
                            toolTipRelays.SetToolTip(labelRelays, string.Join("\n", _nostrAccess.Relays.Select(r => r.ToString())));
                            break;
                        default:
                            labelRelays.Text = $"{_nostrAccess.Relays.Length} relays";
                            toolTipRelays.SetToolTip(labelRelays, string.Join("\n", _nostrAccess.Relays.Select(r => r.ToString())));
                            break;
                    }
                    if (null != _nostrAccess.Clients)
                    {
                        _nostrAccess.Clients.EventsReceived += OnClientOnEventsReceived;
                    }
                }

                if (0 == connectCount)
                {
                    textBoxTimeline.Text = "> No relay enabled." + Environment.NewLine + textBoxTimeline.Text;
                    return;
                }

                textBoxTimeline.Text = string.Empty;
                textBoxTimeline.Text = "> Connect." + Environment.NewLine + textBoxTimeline.Text;

                _nostrAccess.Subscribe();

                buttonStart.Enabled = false;
                buttonStop.Enabled = true;
                buttonStop.Focus();
                textBoxTimeline.Text = "> Create subscription." + Environment.NewLine + textBoxTimeline.Text;

                // ���O�C���ς݂̎�
                if (!string.IsNullOrEmpty(_npubHex))
                {
                    // �t�H���C�[���w�ǂ�����
                    _nostrAccess.SubscribeFollows(_npubHex);

                    // ���O�C�����[�U�[�\�����擾
                    var name = GetUserName(_npubHex);
                    textBoxTimeline.Text = $"> Login as {name}." + Environment.NewLine + textBoxTimeline.Text;
                }
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
                textBoxTimeline.Text = "> Could not start." + Environment.NewLine + textBoxTimeline.Text;
            }
        }
        #endregion

        #region �C�x���g��M������
        /// <summary>
        /// �C�x���g��M������
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnClientOnEventsReceived(object? sender, (string subscriptionId, NostrEvent[] events) args)
        {
            // �^�C�����C���w��
            if (args.subscriptionId == _nostrAccess.SubscriptionId)
            {
                foreach (var nostrEvent in args.events)
                {
                    if (RemoveCompletedEventIds(nostrEvent.Id))
                    {
                        continue;
                    }

                    var content = nostrEvent.Content;
                    if (content != null)
                    {
                        // ���ԕ\��
                        DateTimeOffset time;
                        int hour;
                        int minute;
                        string timeString = "- ";
                        if (nostrEvent.CreatedAt != null)
                        {
                            time = (DateTimeOffset)nostrEvent.CreatedAt;
                            time = time.LocalDateTime;
                            hour = time.Hour;
                            minute = time.Minute;
                            timeString = string.Format("{0:D2}", hour) + ":" + string.Format("{0:D2}", minute);
                        }

                        // �t�H���C�[�`�F�b�N
                        string headMark = "-";
                        string speaker = "\\1";
                        if (_followeesHexs.Contains(nostrEvent.PublicKey))
                        {
                            headMark = "*";
                            // �{�̑�������ׂ�
                            speaker = "\\0";
                        }

                        // ���A�N�V����
                        if (7 == nostrEvent.Kind)
                        {
                            // ���O�C���ς݂Ŏ����ւ̃��A�N�V����
                            if (!string.IsNullOrEmpty(_npubHex) && nostrEvent.GetTaggedPublicKeys().Contains(_npubHex))
                            {
                                Users.TryGetValue(nostrEvent.PublicKey, out User? user);
                                // ���[�U�[�\�����擾
                                string userName = GetUserName(nostrEvent.PublicKey);
                                // ���[�U�[�\�����J�b�g
                                if (userName.Length > _cutNameLength)
                                {
                                    userName = $"{userName[.._cutNameLength]}...";
                                }

                                // SSP�ɑ���
                                if (null != _ds)
                                {
                                    NIP19.NostrEventNote nostrEventNote = new()
                                    {
                                        EventId = nostrEvent.Id,
                                        Relays = [string.Empty],
                                    };
                                    var nevent = nostrEventNote.ToNIP19();
                                    //SearchGhost();
                                    _ds.Update();
                                    Dictionary<string, string> SSTPHeader = new(_baseSSTPHeader)
                                    {
                                        { "Reference1", $"{nostrEvent.Kind}" }, // kind
                                        { "Reference2", content }, // content
                                        { "Reference3", user?.Name ?? "???" }, // name
                                        { "Reference4", user?.DisplayName ?? string.Empty }, // display_name
                                        { "Reference5", user?.Picture ?? string.Empty }, // picture
                                        { "Reference6", nevent }, // nevent1...
                                        { "Reference7", nostrEvent.PublicKey.ConvertToNpub() }, // npub1...
                                        { "Script", $"{speaker}���A�N�V���� {userName}\\n{content}\\e" }
                                    };
                                    string sstpmsg = _SSTPMethod + "\r\n" + string.Join("\r\n", SSTPHeader.Select(kvp => kvp.Key + ": " + kvp.Value.Replace("\n", "\\n"))) + "\r\n\r\n";
                                    string r = _ds.GetSSTPResponse(_ghostName, sstpmsg);
                                    //Debug.WriteLine(r);
                                }
                                // ��ʂɕ\��
                                textBoxTimeline.Text = "+" + timeString + " " + userName + " " + content + Environment.NewLine + textBoxTimeline.Text;
                            }
                        }
                        // �e�L�X�g�m�[�g
                        if (1 == nostrEvent.Kind || 42 == nostrEvent.Kind)
                        {
                            if (42 == nostrEvent.Kind)
                            {
                                headMark = "=";
                            }

                            var userClient = nostrEvent.GetTaggedData("client");
                            var iSnokakoi = -1 < Array.IndexOf(userClient, "nokakoi");

                            // �t�H���C�[����\���I���Ńt�H���C�[����Ȃ����͕\�����Ȃ�
                            if (_showOnlyFollowees && !_followeesHexs.Contains(nostrEvent.PublicKey))
                            {
                                continue;
                            }

                            // �~���[�g���Ă��鎞�͕\�����Ȃ�
                            if (IsMuted(nostrEvent.PublicKey))
                            {
                                continue;
                            }

                            Users.TryGetValue(nostrEvent.PublicKey, out User? user);
                            // ���[�U�[�\�����擾�i���[�U�[�����������ߖ�̂��߁��̃t���O������Ɂj
                            string userName = GetUserName(nostrEvent.PublicKey);

                            bool isSole = false;
                            foreach (SoleGhost soleGhost in _soleGhosts)
                            {
                                if (soleGhost.Npub == nostrEvent.PublicKey.ConvertToNpub())
                                {
                                    isSole = true;
                                    break;
                                }
                            }

                            // ���[�U�[��������Ȃ����͕\�����Ȃ�
                            if (null == user)
                            {
                                continue;
                            }

                            // ���[�U�[�\�����J�b�g
                            if (userName.Length > _cutNameLength)
                            {
                                userName = $"{userName[.._cutNameLength]}...";
                            }

                            // SSP�ɑ���
                            if (null != _ds)
                            {
                                NIP19.NostrEventNote nostrEventNote = new()
                                {
                                    EventId = nostrEvent.Id,
                                    Relays = [string.Empty],
                                };
                                var nevent = nostrEventNote.ToNIP19();
                                //SearchGhost();
                                _ds.Update();

                                string msg = content;
                                // �{���J�b�g
                                if (msg.Length > _cutLength)
                                {
                                    msg = $"{msg[.._cutLength]}...";
                                }
                                Dictionary<string, string> SSTPHeader = new(_baseSSTPHeader)
                                {
                                    { "Reference1", $"{nostrEvent.Kind}" },
                                    { "Reference2", content }, // content
                                    { "Reference3", user?.Name ?? "???" }, // name
                                    { "Reference4", user?.DisplayName ?? string.Empty }, // display_name
                                    { "Reference5", user?.Picture ?? string.Empty }, // picture
                                    { "Reference6", nevent }, // nevent1...
                                    { "Reference7", nostrEvent.PublicKey.ConvertToNpub() }, // npub1...
                                    { "Script", $"{speaker}{(isSole ? "" : userName)}\\n{msg}\\e" }
                                };
                                string sstpmsg = _SSTPMethod + "\r\n" + string.Join("\r\n", SSTPHeader.Select(kvp => kvp.Key + ": " + kvp.Value.Replace("\n", "\\n"))) + "\r\n\r\n";

                                string r;
                                foreach (SoleGhost soleGhost in _soleGhosts)
                                {
                                    if (soleGhost.Npub == nostrEvent.PublicKey.ConvertToNpub())
                                    {
                                        r = _ds.GetSSTPResponse(soleGhost.GhostName, sstpmsg);
                                        Debug.WriteLine(r);
                                    }
                                }

                                if (!isSole && !_soleGhostsOnly)
                                {
                                    r = _ds.GetSSTPResponse(_ghostName, sstpmsg);
                                    Debug.WriteLine(r);
                                }
                            }

                            // �L�[���[�h�ʒm
                            var settings = Notifier.Settings;
                            if (Notifier.CheckPost(content) && settings.Open)
                            {
                                NIP19.NostrEventNote nostrEventNote = new()
                                {
                                    EventId = nostrEvent.Id,
                                    Relays = [string.Empty],
                                };
                                var nevent = nostrEventNote.ToNIP19();
                                var app = new ProcessStartInfo
                                {
                                    FileName = settings.FileName + nevent,
                                    UseShellExecute = true
                                };
                                try
                                {
                                    Process.Start(app);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine(ex.Message);
                                }
                            }

                            // ���s���X�y�[�X�ɒu������
                            content = content.Replace('\n', ' ');
                            // �{���J�b�g
                            if (content.Length > _cutLength)
                            {
                                content = $"{content[.._cutLength]}...";
                            }
                            // ��ʂɕ\��
                            textBoxTimeline.Text = (iSnokakoi ? "[n]" : string.Empty) + headMark
                                                 + $"{timeString} {userName}{Environment.NewLine}"
                                                 + " " + content + Environment.NewLine + textBoxTimeline.Text;
                            Debug.WriteLine($"{timeString} {userName} {content}");
                        }
                    }
                }
            }
            // �t�H���C�[�w��
            else if (args.subscriptionId == _nostrAccess.GetFolloweesSubscriptionId)
            {
                foreach (var nostrEvent in args.events)
                {
                    // �t�H���[���X�g
                    if (3 == nostrEvent.Kind)
                    {
                        var tags = nostrEvent.Tags;
                        foreach (var tag in tags)
                        {
                            // ���J����ۑ�
                            if ("p" == tag.TagIdentifier)
                            {
                                // �擪�����J���ƌ��߂��Ă��邪�c
                                _followeesHexs.Add(tag.Data[0]);
                            }
                        }
                    }
                }
            }
            // �v���t�B�[���w��
            else if (args.subscriptionId == _nostrAccess.GetProfilesSubscriptionId)
            {
                foreach (var nostrEvent in args.events)
                {
                    if (RemoveCompletedEventIds(nostrEvent.Id))
                    {
                        continue;
                    }

                    // �v���t�B�[��
                    if (0 == nostrEvent.Kind && null != nostrEvent.Content && null != nostrEvent.PublicKey)
                    {
                        var newUserData = Tools.JsonToUser(nostrEvent.Content, nostrEvent.CreatedAt, Notifier.Settings.MuteMostr);
                        if (null != newUserData)
                        {
                            DateTimeOffset? cratedAt = DateTimeOffset.MinValue;
                            if (Users.TryGetValue(nostrEvent.PublicKey, out User? existingUserData))
                            {
                                cratedAt = existingUserData?.CreatedAt;
                            }
                            if (false == existingUserData?.Mute)
                            {
                                // ���Ƀ~���[�g�I�t��Mostr�A�J�E���g�̃~���[�g������
                                newUserData.Mute = false;
                            }
                            if (null == cratedAt || cratedAt < newUserData.CreatedAt)
                            {
                                newUserData.LastActivity = DateTime.Now;
                                Tools.SaveUsers(Users);
                                // �����ɒǉ��i�㏑���j
                                Users[nostrEvent.PublicKey] = newUserData;
                                Debug.WriteLine($"cratedAt updated {cratedAt} -> {newUserData.CreatedAt}");
                                Debug.WriteLine($"�v���t�B�[���X�V {newUserData.LastActivity} {newUserData.DisplayName} {newUserData.Name}");
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Stop�{�^��
        // Stop�{�^��
        private void ButtonStop_Click(object sender, EventArgs e)
        {
            if (null != _nostrAccess.Clients)
            {
                try
                {
                    _nostrAccess.CloseSubscriptions();
                    textBoxTimeline.Text = "> Close subscription." + Environment.NewLine + textBoxTimeline.Text;

                    _ = _nostrAccess.Clients.Disconnect();
                    textBoxTimeline.Text = "> Disconnect." + Environment.NewLine + textBoxTimeline.Text;
                    _nostrAccess.Clients.Dispose();
                    _nostrAccess.Clients = null;

                    buttonStart.Enabled = true;
                    buttonStart.Focus();
                    buttonStop.Enabled = false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ButtonStop_Click: " + ex.Message);
                    Debug.Print(ex.ToString());
                    textBoxTimeline.Text = "> Could not stop." + Environment.NewLine + textBoxTimeline.Text;
                }
            }
        }
        #endregion

        #region Setting�{�^��
        // Setting�{�^��
        private async void ButtonSetting_Click(object sender, EventArgs e)
        {
            // �J���O
            _formSetting.checkBoxTopMost.Checked = TopMost;
            _formSetting.textBoxCutLength.Text = _cutLength.ToString();
            _formSetting.textBoxCutNameLength.Text = _cutNameLength.ToString();
            _formSetting.trackBarOpacity.Value = (int)(Opacity * 100);
            _formSetting.checkBoxShowOnlyFollowees.Checked = _showOnlyFollowees;
            _formSetting.textBoxNpub.Text = _npub;
            _formSetting.textBoxPreferredGhost.Text = _ghostName;
            _formSetting.checkBoxSoleGhostsOnly.Checked = _soleGhostsOnly;

            // �J��
            _formSetting.ShowDialog(this);

            // ������
            TopMost = _formSetting.checkBoxTopMost.Checked;
            if (!int.TryParse(_formSetting.textBoxCutLength.Text, out _cutLength))
            {
                _cutLength = 40;
            }
            else if (_cutLength < 1)
            {
                _cutLength = 1;
            }
            if (!int.TryParse(_formSetting.textBoxCutNameLength.Text, out _cutNameLength))
            {
                _cutNameLength = 8;
            }
            else if (_cutNameLength < 1)
            {
                _cutNameLength = 1;
            }
            Opacity = _formSetting.trackBarOpacity.Value / 100.0;
            _showOnlyFollowees = _formSetting.checkBoxShowOnlyFollowees.Checked;
            _ghostName = _formSetting.textBoxPreferredGhost.Text;
            _npub = _formSetting.textBoxNpub.Text;
            _soleGhostsOnly = _formSetting.checkBoxSoleGhostsOnly.Checked;
            try
            {
                // �ʃA�J�E���g���O�C�����s�ɔ����ăN���A���Ă���
                _npubHex = string.Empty;
                _followeesHexs.Clear();

                // ���J���擾
                _npubHex = _npub.ConvertToHex();

                // ���O�C���ς݂̎�
                if (!string.IsNullOrEmpty(_npubHex))
                {
                    int connectCount = await _nostrAccess.ConnectAsync();
                    if (0 == connectCount)
                    {
                        textBoxTimeline.Text = "> No relay enabled." + Environment.NewLine + textBoxTimeline.Text;
                        return;
                    }

                    // �t�H���C�[���w�ǂ�����
                    _nostrAccess.SubscribeFollows(_npubHex);

                    // ���O�C�����[�U�[�\�����擾
                    var name = GetUserName(_npubHex);
                    textBoxTimeline.Text = $"> Login as {name}." + Environment.NewLine + textBoxTimeline.Text;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                textBoxTimeline.Text = "> Wrong npub." + Environment.NewLine + textBoxTimeline.Text;
            }

            Setting.TopMost = TopMost;
            Setting.CutLength = _cutLength;
            Setting.CutNameLength = _cutNameLength;
            Setting.Opacity = Opacity;
            Setting.ShowOnlyFollowees = _showOnlyFollowees;
            Setting.Ghost = _ghostName;
            Setting.SoleGhostsOnly = _soleGhostsOnly;
            Setting.Npub = _npub;

            Setting.Save(_configPath);

            RefleshGhosts();
        }
        #endregion

        #region ���������[����̏����ς݃C�x���g�����O
        /// <summary>
        /// ���������[����̏����ς݃C�x���g�����O
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns>�����ς݃C�x���g�̗L��</returns>
        private bool RemoveCompletedEventIds(string eventId)
        {
            if (_displayedEventIds.Contains(eventId))
            {
                return true;
            }

            if (_displayedEventIds.Count >= 128)
            {
                _displayedEventIds.RemoveFirst();
            }
            _displayedEventIds.AddLast(eventId);
            return false;
        }
        #endregion

        #region ������������
        // �}�E�X��������
        private void TextBoxTimeline_MouseEnter(object sender, EventArgs e)
        {
            _tempOpacity = Opacity;
            Opacity = 1.00;
        }

        // �}�E�X�o����
        private void TextBoxTimeline_MouseLeave(object sender, EventArgs e)
        {
            Opacity = _tempOpacity;
        }
        #endregion

        #region SSP�S�[�X�g�����擾����
        /// <summary>
        /// SSP�S�[�X�g�����擾����
        /// </summary>
        //private void SearchGhost()
        //{
        //    _ds.Update();
        //    SakuraFMO fmo = (SakuraFMO)_ds.FMO;
        //    var names = fmo.GetGhostNames();
        //    if (names.Length > 0)
        //    {
        //        _ghostName = names.First(); // �Ƃ肠�����擪��
        //        //Debug.Print(_ghostName);
        //    }
        //    else
        //    {
        //        _ghostName = string.Empty;
        //        //Debug.Print("�S�[�X�g�����܂���");
        //    }
        //}
        #endregion

        #region ���[�U�[�\�������擾����
        /// <summary>
        /// ���[�U�[�\�������擾����
        /// </summary>
        /// <param name="publicKeyHex">���J��HEX</param>
        /// <returns>���[�U�[�\����</returns>
        private string GetUserName(string publicKeyHex)
        {
            /*
            // �����ɂȂ��ꍇ�v���t�B�[�����w�ǂ���
            if (!_users.TryGetValue(publicKeyHex, out User? user))
            {
                SubscribeProfiles([publicKeyHex]);
            }
            */
            // kind 0 �𖈉�w�ǂ���悤�ɕύX�i�p�ɂ�display_name����ύX���郆�[�U�[�����邽�߁j
            _nostrAccess.SubscribeProfiles([publicKeyHex]);

            // ��񂪂���Ε\�������擾
            Users.TryGetValue(publicKeyHex, out User? user);
            string? userName = "???";
            if (null != user)
            {
                userName = user.DisplayName;
                // display_name�������ꍇ��@name�Ƃ���
                if (null == userName || string.Empty == userName)
                {
                    userName = $"@{user.Name}";
                }
                // �擾���X�V
                user.LastActivity = DateTime.Now;
                Tools.SaveUsers(Users);
                Debug.WriteLine($"���[�U�[���擾 {user.LastActivity} {user.DisplayName} {user.Name}");
            }
            return userName;
        }
        #endregion

        #region �~���[�g����Ă��邩�m�F����
        /// <summary>
        /// �~���[�g����Ă��邩�m�F����
        /// </summary>
        /// <param name="publicKeyHex">���J��HEX</param>
        /// <returns>�~���[�g�t���O</returns>
        private bool IsMuted(string publicKeyHex)
        {
            if (Users.TryGetValue(publicKeyHex, out User? user))
            {
                if (null != user)
                {
                    return user.Mute;
                }
            }
            return false;
        }
        #endregion

        #region ���鎞
        // ���鎞
        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _nostrAccess.CloseSubscriptions();
            _nostrAccess.DisconnectAndDispose();

            if (FormWindowState.Normal != WindowState)
            {
                // �ŏ����ő剻��Ԃ̎��A���̈ʒu�Ƒ傫����ۑ�
                Setting.Location = RestoreBounds.Location;
                Setting.Size = RestoreBounds.Size;
            }
            else
            {
                Setting.Location = Location;
                Setting.Size = Size;
            }
            Setting.Save(_configPath);
            Tools.SaveUsers(Users);
            Notifier.SaveSettings(); // �K�v�Ȃ����X�V���������낦�邽��
            Tools.SaveSoleGhosts(_soleGhosts);

            _ds.Dispose();      // FrmMsgReceiver��Thread��~����1000ms�҂�����邤���Ƀv���Z�X�c��̂Łc
            Application.Exit(); // ������ŎE���BSSTLib�Ɏ����ꂽ�����������A�Ƃ肠�����B
        }
        #endregion

        #region ���[�h��
        // ���[�h��
        private void FormMain_Load(object sender, EventArgs e)
        {
            RefleshGhosts();
            ButtonStart_Click(sender, e);
        }
        #endregion

        #region ��ʕ\���ؑ�
        // ��ʕ\���ؑ�
        private void FormMain_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                ButtonSetting_Click(sender, e);
            }
            if (e.KeyCode == Keys.F10)
            {
                var ev = new MouseEventArgs(MouseButtons.Right, 1, 0, 0, 0);
                FormMain_MouseClick(sender, ev);
            }
        }
        #endregion

        #region �}�j�A�N�X�\��
        private void FormMain_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (null == _formManiacs || _formManiacs.IsDisposed)
                {
                    _formManiacs = new FormManiacs
                    {
                        MainForm = this
                    };
                }
                if (!_formManiacs.Visible)
                {
                    _formManiacs.Show(this);
                }
            }
        }
        #endregion

        #region �����[���X�g�\��
        private void ButtonRelayList_Click(object sender, EventArgs e)
        {
            _formRelayList = new FormRelayList();
            if (_formRelayList.ShowDialog(this) == DialogResult.OK)
            {
                ButtonStop_Click(sender, e);
                ButtonStart_Click(sender, e);
            }
            _formRelayList.Dispose();
        }
        #endregion

        private static void SearchGhost(ComboBox comboBox, string? ghost)
        {
            comboBox.Items.Clear();
            SakuraFMO fmo = new("SakuraUnicode");
            fmo.Update(true);
            string[] names = fmo.GetGhostNames();
            if (names.Length > 0)
            {
                comboBox.Items.AddRange(names);
                comboBox.SelectedIndex = 0;
                if (!string.IsNullOrEmpty(ghost))
                {
                    comboBox.SelectedItem = ghost;
                }
            }
        }

        private void RefleshGhosts()
        {
            _soleGhosts = Tools.LoadSoleGhosts();
            if (_soleGhosts.Count < 2)
            {
                while (_soleGhosts.Count < 2)
                {
                    _soleGhosts.Add(new SoleGhost());
                }
            }
            textBoxGhostNpub1.Text = _soleGhosts[0].Npub;
            SearchGhost(comboBoxGhosts1, _soleGhosts[0].GhostName);
            textBoxGhostNpub2.Text = _soleGhosts[1].Npub;
            SearchGhost(comboBoxGhosts2, _soleGhosts[1].GhostName);
        }

        private void TextBoxGhostNpub1_TextChanged(object sender, EventArgs e)
        {
            _soleGhosts[0].Npub = textBoxGhostNpub1.Text;
            Tools.SaveSoleGhosts(_soleGhosts);
        }

        private void TextBoxGhostNpub2_TextChanged(object sender, EventArgs e)
        {
            _soleGhosts[1].Npub = textBoxGhostNpub2.Text;
            Tools.SaveSoleGhosts(_soleGhosts);
        }

        private void ComboBoxGhosts1_SelectionChangeCommitted(object sender, EventArgs e)
        {
            _soleGhosts[0].GhostName = comboBoxGhosts1.Text;
            Tools.SaveSoleGhosts(_soleGhosts);
        }

        private void ComboBoxGhosts2_SelectionChangeCommitted(object sender, EventArgs e)
        {
            _soleGhosts[1].GhostName = comboBoxGhosts2.Text;
            Tools.SaveSoleGhosts(_soleGhosts);
        }
    }
}
