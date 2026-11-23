// BGM(画面ごとの音楽) (Pygame 版の bgm.py)
//
//   assets/bgm/<フォルダ>/ に音楽ファイルを1つ入れると、その画面に切りかわったときに流れる(くり返し)
//     (Unity では Assets/StreamingAssets/assets/bgm/<フォルダ>/)
//     フォルダ … Title / Menu / Battle(GameData.SCENE_FOLDERS)
//   音量は data/bgm.csv で画面ごとに決める(「画面」は タイトル / メニュー / バトル。GameData.BGM_SCENES)
//
//   ・画面が切りかわると、前の曲を小さくしながら止めて、次の曲を大きくしながら流す(フェード)
//   ・次の画面も同じ曲なら、止めずにそのまま流しつづける
//   ・その画面のフォルダに曲がなければ、音楽を止める
//   ・Unity で流せるのは .ogg / .mp3 / .wav (.aif / .aiff)。.flac と .opus は流せない
//
// 新しい画面に BGM をつけるときは
//   1. GameData.cs の SCENE_FOLDERS に「画面の名前: フォルダ名(英語)」、BGM_SCENES に「画面の名前: 表の画面名」を足す
//   2. assets/bgm にそのフォルダを作り、data/bgm.csv に音量の行を足す
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

namespace QteRpg.UnityPort
{
    public sealed class BgmPlayer : IBgmPlayer, IDisposable
    {
        const double FADE_TIME = 0.6;  // 曲を切りかえるときのフェードの時間(秒)

        // 使える音楽ファイル → Unity での種類
        static readonly Dictionary<string, AudioType> AUDIO_EXTS = new Dictionary<string, AudioType>
        {
            { ".ogg", AudioType.OGGVORBIS }, { ".mp3", AudioType.MPEG }, { ".wav", AudioType.WAV },
            { ".aif", AudioType.AIFF }, { ".aiff", AudioType.AIFF },
        };
        static readonly string[] UNSUPPORTED_EXTS = { ".flac", ".opus" };  // Pygame 版では使えたが Unity では流せない

        static string BgmDir => Path.Combine(Settings.BASE_DIR, "assets", "bgm");

        AudioSource _source;
        bool _enabled = true;
        string _current;        // 今流している曲(ファイルの場所)
        string _currentId;      // 今流している曲の印(TrackId)
        string _nextTrack;      // フェードアウトのあとに流す曲
        double _nextVolume = 1.0;
        double _volume;         // 今の音量(0〜1)
        double _targetVolume;   // 目指す音量(0〜1)
        bool _fadingOut;
        UnityWebRequest _loading;   // 読みこみ中の曲
        AudioClip _clip;

        /// <param name="host">音を鳴らす AudioSource をつける GameObject</param>
        public BgmPlayer(GameObject host)
        {
            try
            {
                _source = host.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.loop = true;  // くり返し
                _source.volume = 0f;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[BGM] 音が出せないので、BGM なしで続けます: {error.Message}");
                _enabled = false;
            }
        }

        /// <summary>画面のフォルダにある曲を1つ返す(なければ null)。いくつもあるときは名前順で最初の曲</summary>
        static string FindTrack(string sceneName)
        {
            if (!GameData.SCENE_FOLDERS.TryGetValue(sceneName, out string folderName))
            {
                return null;
            }
            string folder = Path.Combine(BgmDir, folderName);
            if (!Directory.Exists(folder))
            {
                return null;
            }
            var files = Directory.GetFiles(folder).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
            var tracks = files.Where(path => AUDIO_EXTS.ContainsKey(Path.GetExtension(path).ToLowerInvariant())).ToList();
            if (tracks.Count == 0)
            {
                var unsupported = files.Where(path => UNSUPPORTED_EXTS.Contains(Path.GetExtension(path).ToLowerInvariant())).ToList();
                if (unsupported.Count > 0)
                {
                    Debug.LogWarning($"[BGM] 「{folderName}」の {Path.GetFileName(unsupported[0])} は Unity では流せません。.ogg か .mp3 にしてください");
                }
                return null;
            }
            if (tracks.Count > 1)
            {
                Debug.Log($"[BGM] 「{folderName}」に曲が {tracks.Count}個あるので、「{Path.GetFileName(tracks[0])}」を流します");
            }
            return tracks[0];
        }

        /// <summary>
        /// 同じ曲かどうかを見分けるための印(ファイルの大きさ + 中身の先頭)
        /// 別のフォルダに同じ曲をコピーしても「同じ曲」とみなす
        /// </summary>
        static string TrackId(string track)
        {
            try
            {
                using (var file = File.OpenRead(track))
                using (var sha1 = SHA1.Create())
                {
                    var head = new byte[Math.Min(1024 * 1024, file.Length)];
                    int read = 0;
                    while (read < head.Length)
                    {
                        int n = file.Read(head, read, head.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    return file.Length + ":" + BitConverter.ToString(sha1.ComputeHash(head, 0, read));
                }
            }
            catch (IOException)
            {
                return track;
            }
        }

        /// <summary>画面が切りかわったときに呼ぶ。その画面の曲に切りかえる</summary>
        public void PlayScene(string sceneName)
        {
            if (!_enabled)
            {
                return;
            }
            string track = FindTrack(sceneName);
            double volume = GameData.BGM_VOLUMES.GetOr(GameData.BGM_SCENES.GetOr(sceneName, null), 1.0);
            if (track != null && _current != null && TrackId(track) == _currentId)
            {
                // 同じ曲 → 止めずに流しつづける(音量だけ合わせる)
                _fadingOut = false;
                _nextTrack = null;
                _targetVolume = volume;
                return;
            }
            _nextTrack = track;
            _nextVolume = volume;
            if (_current == null)
            {
                StartNext();
            }
            else
            {
                _fadingOut = true;  // 今の曲を小さくしてから切りかえる(Update で進める)
            }
        }

        /// <summary>毎フレーム呼ぶ。フェードを進める(Game.Step から呼ばれる)</summary>
        public void Update(double dt)
        {
            if (!_enabled || _current == null)
            {
                return;
            }
            PollLoading();
            double step = dt / FADE_TIME;
            if (_fadingOut)
            {
                _volume = Math.Max(0.0, _volume - step);
                if (_volume <= 0 || _loading != null)
                {
                    StopCurrent();
                    _fadingOut = false;
                    StartNext();
                    return;
                }
            }
            else if (_loading != null)
            {
                return;  // 読みこみ中は音量を上げはじめない
            }
            else if (_volume < _targetVolume)
            {
                _volume = Math.Min(_targetVolume, _volume + step);
            }
            else if (_volume > _targetVolume)
            {
                _volume = Math.Max(_targetVolume, _volume - step);
            }
            _source.volume = (float)_volume;
        }

        void StartNext()
        {
            string track = _nextTrack;
            _nextTrack = null;
            if (track == null)
            {
                return;
            }
            AudioType type = AUDIO_EXTS[Path.GetExtension(track).ToLowerInvariant()];
            try
            {
                var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(track).AbsoluteUri, type);
                ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio = false;
                request.SendWebRequest();
                _loading = request;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[BGM] 曲を流せませんでした: {track}: {error.Message}");
                StopCurrent();
                return;
            }
            _current = track;
            _currentId = TrackId(track);
            _volume = 0.0;
            _targetVolume = _nextVolume;  // 0 から少しずつ大きくする(フェードイン)
            _source.volume = 0f;
        }

        /// <summary>読みこみが終わった曲を流しはじめる</summary>
        void PollLoading()
        {
            if (_loading == null || !_loading.isDone)
            {
                return;
            }
            var request = _loading;
            _loading = null;
            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[BGM] 曲を流せませんでした: {_current}: {request.error}");
                    StopCurrent();
                    return;
                }
                _clip = DownloadHandlerAudioClip.GetContent(request);
                _clip.name = Path.GetFileName(_current);
                _source.clip = _clip;
                _source.volume = 0f;
                _source.Play();
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[BGM] 曲を流せませんでした: {_current}: {error.Message}");
                StopCurrent();
            }
            finally
            {
                request.Dispose();
            }
        }

        void StopCurrent()
        {
            if (_loading != null)
            {
                _loading.Abort();
                _loading.Dispose();
                _loading = null;
            }
            if (_source != null)
            {
                _source.Stop();
                _source.clip = null;
            }
            if (_clip != null)
            {
                UnityEngine.Object.Destroy(_clip);
                _clip = null;
            }
            _current = null;
            _currentId = null;
            _volume = 0.0;
        }

        public void Dispose()
        {
            StopCurrent();
        }
    }
}
