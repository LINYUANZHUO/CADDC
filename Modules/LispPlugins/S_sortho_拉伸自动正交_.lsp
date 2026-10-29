;;; ============================================================
;;; sortho.lsp —— S 拉伸自动开正交插件
;;;
;;; 功能：输入 S（拉伸 STRETCH）时自动打开正交开关；
;;;       命令过程中仍可随时按 F8 关闭/打开正交，不会锁死；
;;;       拉伸结束后正交自动还原到执行前的状态（开还原开，关还原关）。
;;;
;;; 加载方法：AutoCAD 命令行输入 APPLOAD，选择本文件，点击"加载"
;;; 想每次启动自动生效：APPLOAD → "启动组" → "内容" → 添加本文件
;;;
;;; 说明：
;;;   - 只对键盘输入 S 生效；点功能区/菜单里的"拉伸"按钮
;;;     不会触发自动开正交
;;;   - 正交还原时机：拉伸命令正常结束或中断（ESC）时还原到
;;;     执行前的状态；若 F8 期间手动切换，结束后也会被还原
;;; ============================================================

;; ---------- S = 拉伸 + 自动正交（结束后还原） ----------
(defun c:S (/ *error* oldortho)

  ;; 出错（如按 ESC 中断）时也要把正交还原
  (defun *error* (msg)
    (if oldortho (setvar "ORTHOMODE" oldortho))
    (if (and msg (not (wcmatch (strcase msg) "*BREAK*,*CANCEL*,*QUIT*")))
      (princ (strcat "\n错误: " msg)))
    (princ))

  ;; 记住正交原来的状态，再自动打开
  (setq oldortho (getvar "ORTHOMODE"))
  (setvar "ORTHOMODE" 1)

  ;; 执行拉伸：新版 AutoCAD 用 command-s 完整交还命令；
  ;; 旧版没有 command-s，则启动命令后直接结束 LISP，
  ;; 命令保持挂起状态，由用户继续正常操作（此情况无法自动还原）。
  (if (member "COMMAND-S" (atoms-family 1))
    (progn
      (command-s "_.STRETCH")
      ;; 命令结束后恢复正交原状态：原来是开就还原为开，是关就还原为关
      (if (zerop (getvar "CMDACTIVE"))
        (setvar "ORTHOMODE" oldortho)))
    (command "_.STRETCH"))
  (princ)
)

(princ "\nS 拉伸自动正交插件已加载：输入 S 拉伸时自动开正交，F8 可随时切换。")
(princ)