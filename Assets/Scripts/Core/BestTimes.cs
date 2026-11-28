// 自己ベストのタイム(3面までクリアしたときの合計タイム。速い順に3位まで)
//
// 3面までクリアすると BattleScene が Add を呼ぶ。3位までに入れば保存する
// メニューの「タイム」で、Load したものを表示する
//
// 保存する場所: Settings.SAVE_DIR の best_times.csv
//   Unity では Application.persistentDataPath(Windows なら C:\Users\<名前>\AppData\LocalLow\<会社名>\<ゲーム名>)
//   ゲームのフォルダとは別の場所なので、ゲームを入れなおしたり新しいバージョンにしたりしても消えない
// ファイルの中身(UTF-8):
//   順位,タイム,1/100秒,日時
//   1,01:23:45,8345,2026/11/28 10:30
//   読むときは「1/100秒」と「日時」の列を使う(順位とタイムの列は、ファイルを開いた人が見やすいように書いているだけ)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace QteRpg
{
    /// <summary>自己ベストの記録1つ</summary>
    public sealed class BestTime
    {
        public long Centis;   // タイム(1/100秒の数)
        public string Date;   // 記録した日時("2026/11/28 10:30")
    }

    public static class BestTimes
    {
        public const int MAX = 3;  // 何位まで残すか
        const string FILE_NAME = "best_times.csv";
        const string CENTIS_COLUMN = "1/100秒";
        const string DATE_COLUMN = "日時";

        public static string FilePath => Path.Combine(Settings.SAVE_DIR, FILE_NAME);

        /// <summary>保存してある自己ベストを読む(速い順に MAX 個まで)。ファイルがない・読めないときは空</summary>
        public static List<BestTime> Load()
        {
            var list = new List<BestTime>();
            try
            {
                if (!File.Exists(FilePath))
                {
                    return list;
                }
                var records = Csv.Parse(File.ReadAllText(FilePath, Encoding.UTF8));
                if (records.Count == 0)
                {
                    return list;
                }
                var header = records[0].Select(name => name.Trim().TrimStart('\uFEFF')).ToList();
                int centisColumn = header.IndexOf(CENTIS_COLUMN), dateColumn = header.IndexOf(DATE_COLUMN);
                if (centisColumn < 0)
                {
                    return list;
                }
                foreach (var record in records.Skip(1))
                {
                    if (centisColumn >= record.Count ||
                        !long.TryParse(record[centisColumn].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long centis) ||
                        centis < 0)
                    {
                        continue;  // 読めない行は飛ばす
                    }
                    string date = dateColumn >= 0 && dateColumn < record.Count ? record[dateColumn].Trim() : "";
                    list.Add(new BestTime { Centis = centis, Date = date });
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Log.Warning($"[自己ベストのタイムを読めませんでした] {error.Message}");
            }
            return list.OrderBy(best => best.Centis).Take(MAX).ToList();  // 同じタイムなら前からある記録が上
        }

        /// <summary>
        /// 新しいタイム(秒)を入れて保存する。戻り値: 何位に入ったか(1〜MAX)。入らなければ null
        /// 同じタイムの記録がすでにあるときは、前からある記録のほうを上にする
        /// </summary>
        public static int? Add(double seconds, DateTime? now = null)
        {
            var list = Load();
            var entry = new BestTime
            {
                Centis = Utils.ToCentis(seconds),  // 画面に出すタイムと同じ(1/100秒 より細かいところは切り捨て)
                Date = (now ?? DateTime.Now).ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture),
            };
            int index = list.Count(best => best.Centis <= entry.Centis);
            if (index >= MAX)
            {
                return null;
            }
            list.Insert(index, entry);
            Save(list.Take(MAX).ToList());
            return index + 1;
        }

        /// <summary>ファイルに書く。書いている途中で止まってもこわれないように、別の名前で書いてから入れかえる</summary>
        static void Save(List<BestTime> list)
        {
            try
            {
                Directory.CreateDirectory(Settings.SAVE_DIR);
                var text = new StringBuilder();
                text.Append($"順位,タイム,{CENTIS_COLUMN},{DATE_COLUMN}\r\n");
                for (int i = 0; i < list.Count; i++)
                {
                    text.Append($"{i + 1},{Utils.FormatCentis(list[i].Centis)},{list[i].Centis},{list[i].Date}\r\n");
                }
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, text.ToString(), new UTF8Encoding(true));
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
                File.Move(temp, FilePath);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Log.Warning($"[自己ベストのタイムを保存できませんでした] {error.Message}");
            }
        }
    }
}
