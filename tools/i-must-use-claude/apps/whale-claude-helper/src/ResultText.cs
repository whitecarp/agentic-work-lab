using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WhaleClaudeHelper {
    // Presentation only: no configuration reads, mutations or exception-value forwarding.
    public static class ResultText {
        private static readonly Dictionary<string, string[]> Text = new Dictionary<string, string[]>(StringComparer.Ordinal) {
            {"optimization_complete", new[] {"本轮优化已完成，结果已记录。", "This optimization is complete and its results have been recorded."}},
            {"optimization_partial", new[] {"本轮优化未全部完成，请查看各项说明和备份。", "Some optimization steps did not finish. Review the details and backups."}},
            {"optimization_not_started", new[] {"本轮优化尚未开始，未应用更改。", "This optimization has not started; no changes were applied."}},
            {"optimization_undone", new[] {"本轮优化已撤销，已核验恢复结果。", "This optimization has been undone and the restoration has been verified."}},
            {"undo_partial", new[] {"部分项目未能撤销，请查看说明；发生冲突的现有数据已保留。", "Some items could not be undone. Review the details; conflicting current data was preserved."}},
            {"undo_not_completed", new[] {"撤销未完成，请先查看备份记录再重试。", "Undo did not finish. Review the backup record before trying again."}},
            {"nothing_to_undo", new[] {"当前没有可撤销的优化记录。", "There is no optimization record to undo."}},
            {"operation_unexpected_error", new[] {"操作遇到意外问题，请查看本地记录；不要连续重复应用。", "An unexpected issue interrupted the operation. Review the local record before applying it again."}},
            {"operation_already_running", new[] {"已有操作正在进行，请等待它结束。", "Another operation is already running. Wait for it to finish."}},
            {"close_browsers_and_claude_first", new[] {"请先正常退出浏览器、Claude 桌面端和 Claude Code，再重试。", "Exit your browsers, Claude Desktop and Claude Code normally, then try again."}},
            {"close_claude_first", new[] {"请先正常退出 Claude 桌面端和 Claude Code，再重试。", "Exit Claude Desktop and Claude Code normally, then try again."}},
            {"font_choose_ttf_or_otf", new[] {"请选择 TTF 或 OTF 字体文件。", "Choose a TTF or OTF font file."}},
            {"font_invalid_family", new[] {"无法确认字体名称，未安装这份文件。", "The font family could not be identified. This file was not installed."}},
            {"font_installed_reopen_apps", new[] {"字体已为当前用户安装。重新打开需要使用它的应用后生效。", "The font was installed for your Windows account. Reopen the apps that need it."}},
            {"font_install_failed_rolled_back", new[] {"字体未能安装；已完成回退，或尚未产生更改。", "The font could not be installed. Changes were rolled back, or no changes had been made."}},
            {"font_install_failed_review_backup", new[] {"字体安装未完成，且回退尚未全部完成。请查看备份后再操作。", "Font installation and rollback did not fully finish. Review the backup before continuing."}},
            {"font_system_or_unmanaged_protected", new[] {"这项字体受保护，或不属于可移除的当前用户字体。", "This font is protected or is not an eligible current-user font."}},
            {"font_changed_refresh_list", new[] {"字体在列出后发生了变化。请刷新列表并重新选择。", "The font changed after it was listed. Refresh the list and select it again."}},
            {"font_removed_reopen_apps", new[] {"字体已备份并移入回收站。重新打开相关应用后生效。", "The font was backed up and moved to the Recycle Bin. Reopen affected apps."}},
            {"font_remove_failed_no_loss", new[] {"字体未能移除；原文件已保留，并已尝试恢复原注册状态。", "The font could not be removed. Its original file was retained and its registration was recovered where needed."}},
            {"font_remove_failed_restore_backup", new[] {"字体移除未完全完成，请在备份页核对并恢复。", "Font removal did not fully finish. Review and restore its backup on the Backups page."}},
            {"font_restore_conflict_file_exists", new[] {"原位置已有文件，已跳过恢复以保留现有文件。", "A file already exists at the original location. Restore was skipped to preserve it."}},
            {"font_restored_reopen_apps", new[] {"字体和原注册信息已恢复并核验。请重新打开相关应用。", "The font and its original registration were restored and verified. Reopen affected apps."}},
            {"font_restore_already_matches", new[] {"字体文件和注册信息已与备份完全一致，无需重复恢复；记录已更新。", "The font file and registration already match the backup exactly. No further restoration was needed; the record was updated."}},
            {"font_restore_matches_receipt_unwritten", new[] {"字体文件和注册信息已与备份一致，但未能更新恢复记录；现有字体未改动。", "The font file and registration match the backup, but the restoration record could not be updated. The current font was not changed."}},
            {"font_restore_failed_backup_preserved", new[] {"字体恢复失败；本次更改已回退，备份仍保留。", "Font restoration failed. Its changes were rolled back and the backup was retained."}},
            {"font_restore_failed_review_backup", new[] {"字体恢复和回退未全部完成，请核对备份后再操作。", "Font restoration and rollback did not fully finish. Review the backup before continuing."}},
            {"font_backup_invalid_or_conflict", new[] {"字体备份未通过核验，或现有文件、注册信息发生冲突，已停止恢复。", "Restore stopped because the font backup could not be verified or current files or registrations conflict."}},
            {"font_install_not_active", new[] {"这条安装记录当前无法撤销，可能已经撤销或未完成安装。", "This installation record cannot currently be undone; it may already be undone or incomplete."}},
            {"font_installation_undone", new[] {"这次字体安装已撤销，字体文件已移入回收站。", "This font installation was undone and its font file was moved to the Recycle Bin."}},
            {"font_undo_failed_no_loss", new[] {"本次字体安装未能撤销；原文件和注册信息已保留。", "The font installation could not be undone. Its file and registration were retained."}},
            {"font_undo_failed_review_backup", new[] {"撤销字体安装未完全完成，请查看备份记录。", "Undoing the font installation did not fully finish. Review the backup record."}},
            {"font_user_protected", new[] {"当前用户字体，暂不满足安全移除条件。", "A current-user font that does not meet the requirements for safe removal."}},
            {"font_user_removable", new[] {"当前用户字体，可在备份后移除。", "A current-user font that can be backed up and removed."}},
            {"font_path_unavailable_or_linked", new[] {"字体文件无法访问或经过链接路径，已保护。", "The font is inaccessible or uses a linked path, so it is protected."}},
            {"font_system_protected", new[] {"系统字体，保持不变。", "A system font; it will remain unchanged."}},
            {"font_inventory_unavailable", new[] {"暂时无法完整读取字体列表，请稍后刷新。", "The full font list is temporarily unavailable. Refresh it later."}},
            {"font_registry_conflict", new[] {"已有同名或共用文件的字体注册，未覆盖现有信息。", "An existing font registration uses the same name or file. It was not overwritten."}},
            {"font_registry_changed", new[] {"字体注册信息已变化，或文件被多项共用，已停止操作。", "The font registration changed or its file is shared. The operation stopped."}},
            {"font_registry_missing", new[] {"原字体注册已不存在，请刷新列表。", "The original font registration no longer exists. Refresh the list."}},
            {"font_load_failed", new[] {"Windows 未能载入这份字体。", "Windows could not load this font."}},
            {"font_recycle_incomplete", new[] {"字体未能完整移入回收站，请查看备份记录。", "The font was not fully moved to the Recycle Bin. Review the backup record."}},
            {"font_selection_invalid", new[] {"所选字体信息未通过核验，请刷新后重新选择。", "The selected font could not be verified. Refresh and select it again."}},
            {"font_path_protected", new[] {"该字体路径不在允许修改的当前用户字体目录中。", "This path is outside the current-user font directory that may be changed."}},
            {"font_file_conflict", new[] {"目标位置已有文件，未覆盖。", "The destination already contains a file; it was not overwritten."}},
            {"font_file_still_referenced", new[] {"仍有其他字体注册使用这个文件，已保留。", "Another font registration still uses this file, so it was retained."}},
            {"font_backup_outside_root", new[] {"请选择本工具保存的字体备份。", "Choose a font backup created by this tool."}},
            {"font_manifest_invalid", new[] {"字体备份记录未通过核验，未继续恢复。", "The font backup record could not be verified. Restore did not continue."}},
            {"font_install_manifest_invalid", new[] {"字体安装记录未通过核验，未继续撤销。", "The font installation record could not be verified. Undo did not continue."}},
            {"font_path_invalid", new[] {"字体路径格式不受支持，请重新选择本机文件。", "This font path is unsupported. Select a local file again."}},
            {"font_reparse_protected", new[] {"字体路径包含链接或目录转接，已跳过。", "The font path contains a link or junction and was skipped."}},
            {"font_invalid_size", new[] {"字体文件不存在、为空或超过 32 MB，未处理。", "The font file is missing, empty or larger than 32 MB. It was not processed."}},
            {"font_hash_mismatch", new[] {"字体文件与备份记录不一致，已停止操作。", "The font file does not match its backup record. The operation stopped."}},
            {"close-claude-first", new[] {"请先正常退出 Claude 桌面端和 Claude Code，再重试。", "Exit Claude Desktop and Claude Code normally, then try again."}},
            {"nothing-selected", new[] {"尚未选择要处理的缓存。", "No cache items have been selected."}},
            {"not-allowlisted", new[] {"该项目不在允许清理的范围内，已跳过。", "This item is outside the cleanup allowlist and was skipped."}},
            {"cache-recycled-backup-verified", new[] {"处理成功的缓存已移入回收站，对应备份已核验。", "Successfully processed cache items were moved to the Recycle Bin and their backups were verified."}},
            {"cancelled-backup-retained", new[] {"操作已取消；已建立的备份仍保留。", "The operation was cancelled. Any backups already created were retained."}},
            {"restore-not-allowlisted", new[] {"备份目标不在允许恢复的缓存范围内，已跳过。", "The backup target is outside the allowed cache locations and was skipped."}},
            {"restore-conflict-original-exists", new[] {"原位置已重新产生数据，已跳过恢复，保留现有内容。", "Data has reappeared at the original location. Restore was skipped to preserve it."}},
            {"restore-verified", new[] {"所选缓存已恢复并通过核验。", "The selected cache was restored and verified."}},
            {"restore-finished-with-skips", new[] {"恢复已结束，部分项目已跳过；请查看对应说明。", "Restore finished with some items skipped. Review their details."}},
            {"cancelled", new[] {"操作已取消。", "The operation was cancelled."}},
            {"changed-since-audit", new[] {"项目在检查后发生了变化，请重新检查再选择。", "The item changed after the audit. Audit again before selecting it."}},
            {"source-missing", new[] {"原文件已不存在，未继续处理。", "The original file no longer exists. Processing did not continue."}},
            {"scan-limit", new[] {"项目超出本次安全扫描范围，已跳过。", "The item exceeds the safe scan limits and was skipped."}},
            {"recycle-not-completed", new[] {"未能完整移入回收站，请核对备份后再操作。", "The move to the Recycle Bin did not fully finish. Review the backup before continuing."}},
            {"invalid-backup-location", new[] {"备份位置不受支持，请从本工具的备份列表选择。", "This backup location is unsupported. Select a backup from this tool's list."}},
            {"invalid-backup-manifest", new[] {"备份记录未通过核验，未继续恢复。", "The backup record could not be verified. Restore did not continue."}},
            {"invalid-relative-path", new[] {"备份中的路径格式不安全，已停止处理。", "A path in the backup record is unsafe. Processing stopped."}},
            {"path-outside-root", new[] {"目标超出允许处理的目录，已停止操作。", "The destination is outside the allowed directory. The operation stopped."}},
            {"reparse-point-skipped", new[] {"项目包含链接或目录转接，已跳过。", "The item contains a link or junction and was skipped."}},
            {"content-changed", new[] {"文件内容与核验记录不一致，已停止操作。", "The file contents do not match the verification record. The operation stopped."}},
            {"destination-exists", new[] {"目标位置已有内容，已保留，未覆盖。", "The destination already contains data. It was preserved, not overwritten."}},
            {"backup-permissions-unavailable", new[] {"无法为备份设置仅当前用户可访问的权限，未继续处理。", "The backup could not be restricted to your Windows account. Processing did not continue."}},
            {"access-denied", new[] {"没有访问所需文件的权限，未完成操作。", "Required files could not be accessed. The operation did not finish."}},
            {"path-too-long", new[] {"路径超出当前 Windows 文件操作限制，已停止该项目。请保留备份，再检查较短的恢复位置。", "The path exceeds current Windows file-operation limits. This item stopped. Keep the backup and review a shorter recovery location."}},
            {"unsafe-path", new[] {"路径存在不安全或不明确的格式，已跳过。", "The path is unsafe or ambiguous and was skipped."}},
            {"hardlink-skipped", new[] {"文件存在硬链接，已跳过以保护共享内容。", "The file has a hard link and was skipped to protect shared contents."}},
            {"file-identity-unavailable", new[] {"无法确认文件身份，已停止处理该项目。", "File identity could not be confirmed; this item was stopped."}},
            {"backup-overlaps-cache", new[] {"备份目录位于待清理缓存内，已停止清理。", "The backup directory overlaps a selected cache, so cleanup stopped."}},
            {"file-busy-or-io-error", new[] {"文件可能正在使用，或读写失败。请查看备份后再重试。", "A file may be in use or could not be read or written. Review the backup before retrying."}},
            {"operation-failed", new[] {"操作未完成，请查看本地备份记录。", "The operation did not finish. Review the local backup record."}},
            {"process-check-unavailable", new[] {"无法确认 Claude 是否完全退出，请正常退出后重试。", "Claude's exit status could not be confirmed. Exit it normally and try again."}},
            {"process-check-incomplete", new[] {"尚未确认所有 Claude 进程都已退出，请检查后重试。", "Not all Claude processes could be confirmed as closed. Check and try again."}},
            {"font_target_changed_or_ambiguous", new[] {"个人字体发生变化或无法确认身份，已停止处理。", "The personal font changed or could not be identified. Processing stopped."}},
            {"receipt_update_failed_backup_retained", new[] {"备份仍保留，但结果记录未能更新，请保留备份目录。", "Backups remain, but the receipt could not be updated. Keep the backup folder."}},
            {"invalid_operation_directory", new[] {"优化记录目录未通过核验，未执行撤回。", "The optimization record directory could not be verified. Undo did not run."}},
            {"invalid_step_backup", new[] {"某一步的备份路径未通过核验，已停止。", "A step's backup path could not be verified. Processing stopped."}},
            {"ambiguous_step_backup", new[] {"存在多个可能的步骤备份，已保留全部内容并停止。", "Multiple possible step backups exist. They were kept and processing stopped."}},
            {"invalid_operation_receipt", new[] {"最近的优化记录不完整或已损坏，已停止撤回；不会跳过它撤回更早记录。", "The latest optimization record is incomplete or damaged. Undo stopped without reverting an older operation."}},
            {"invalid_font_target", new[] {"字体目标不在本预设的允许范围内，已停止。", "The font target is outside this preset's allowlist. Processing stopped."}},
            {"operation_receipt_too_large", new[] {"优化记录超出处理限制，已停止继续修改。", "The optimization record exceeds the processing limit. Further changes stopped."}},
            {"invalid_local_path", new[] {"本地路径未通过核验，已停止。", "The local path could not be verified. Processing stopped."}},
            {"access_denied", new[] {"Windows 未允许访问或修改，操作没有完整完成。", "Windows denied access or modification. The operation did not fully finish."}},
            {"file_operation_failed", new[] {"文件读写未完成，已保留能保留的备份，请查看记录。", "A file operation failed. Available backups are retained; review the record."}},
            {"operation_failed", new[] {"操作未完成，请查看本地备份记录。", "The operation did not finish. Review the local backup record."}}
        };
        public static string Render(string message, bool english) {
            if (String.IsNullOrWhiteSpace(message)) return "";
            if (message.Length > 32768) return english ? "The result is lengthy. Review the local backup record." : "结果内容较多，请查看本地备份记录。";
            string[] lines = message.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var output = new List<string>();
            foreach (string value in lines) {
                if (output.Count >= 100) { output.Add(english ? "More details are available in the local record." : "更多信息请查看本地记录。"); break; }
                string line = value.Trim(); if (line.Length == 0) continue;
                output.Add(RenderLine(line, english));
            }
            return String.Join(Environment.NewLine, output);
        }
        private static string RenderLine(string line, bool english) {
            string[] words;
            if (Text.TryGetValue(line, out words)) return words[english ? 1 : 0];
            if (line.StartsWith("close-claude-first: ", StringComparison.Ordinal)) {
                if (line.IndexOf("process-check-unavailable", StringComparison.Ordinal) >= 0 || line.IndexOf("process-check-incomplete", StringComparison.Ordinal) >= 0) return Text["process-check-unavailable"][english ? 1 : 0];
                return Text["close-claude-first"][english ? 1 : 0];
            }
            int colon = line.LastIndexOf(": ", StringComparison.Ordinal);
            if (colon > 0 && Text.TryGetValue(line.Substring(colon + 2), out words)) {
                // Cache labels may contain profile identifiers; keep only the product name.
                string label = line.StartsWith("Claude Code / ", StringComparison.Ordinal) ? "Claude Code" : line.StartsWith("Claude Desktop / ", StringComparison.Ordinal) ? (english ? "Claude Desktop" : "Claude 桌面端") : "";
                return (label.Length == 0 ? "" : label + (english ? ": " : "：")) + words[english ? 1 : 0];
            }
            int divider = line.IndexOf(" / ", StringComparison.Ordinal);
            // Labels such as Chrome / Local State contain the same separator. Find the
            // first divider following Chinese text, rather than splitting the label.
            while(divider>=0&&!Regex.IsMatch(line.Substring(0,divider),@"[\u3400-\u9fff]"))divider=line.IndexOf(" / ",divider+3,StringComparison.Ordinal);
            bool hasPath = Regex.IsMatch(line, @"[A-Za-z]:[\\/]|\\\\|https?://", RegexOptions.CultureInvariant) || line.StartsWith("/", StringComparison.Ordinal);
            if (!hasPath && divider > 0 && divider + 3 < line.Length && Regex.IsMatch(line.Substring(0, divider), @"[\u3400-\u9fff]")) {
                return english ? line.Substring(divider + 3).Trim() : line.Substring(0, divider).Trim();
            }
            // Unknown diagnostic values are never echoed; they may contain private data.
            return english ? "An unrecognized result was returned. Review the local record." : "返回了未识别的结果，请查看本地记录。";
        }
    }
}
