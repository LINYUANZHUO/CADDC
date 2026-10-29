;;; =====================================================================
;;;  开关面板绘制程序  SwitchPanel.lsp
;;;  命令: CCC
;;;  适用: AutoCAD 2002 及以上 (AutoLISP)
;;;  单位: 毫米, 建议 1:1 绘制
;;;  说明: 固定 86x86 规格, 外框四周 3mm 圆角倒角;
;;;        内部只画按键矩形(无装饰框);
;;;        默认 1 联 / 不标注文字 / 旋转角 0, 一次回车直接进入连续插入;
;;;        插入过程中可用 N(联数) / T(文字标注) / R(角度) 随时改设置;
;;;        连续点击鼠标插入, 回车或 ESC 退出。
;;; =====================================================================

;; ------------------- 可修改的默认参数 -------------------
(setq *SP:LAYER* "开关面板")   ; 图层名(不存在则自动创建)
(setq *SP:COLOR* 4)            ; 图层颜色 4=青色
(setq *SP:N*     1)            ; 默认开关联数 1~4
(setq *SP:W*     86.0)         ; 面板宽度 mm (固定 86 型)
(setq *SP:H*     86.0)         ; 面板高度 mm (固定 86 型)
(setq *SP:R*     3.0)          ; 面板四周倒角(圆角)半径 mm
(setq *SP:ANG*   0.0)          ; 默认旋转角度(度)
(setq *SP:TXT*   nil)          ; 是否标注联数文字: nil=否 / T=是

;; ------------------- 内部函数 -------------------

;; 错误处理
(defun sp:err (msg)
  (if (and msg (not (wcmatch (strcase msg) "*CANCEL*,*QUIT*,*EXIT*")))
    (princ (strcat "\n[CCC] 中断: " msg))
  )
  (if *SP:OLDERR* (setq *error* *SP:OLDERR*))
  (if *SP:OLDECHO* (setvar "cmdecho" *SP:OLDECHO*))
  (princ)
)

;; 创建图层
(defun sp:mklayer (nm cl)
  (if (not (tblsearch "LAYER" nm))
    (entmake (list '(0 . "LAYER")
                   '(100 . "AcDbSymbolTableRecord")
                   '(100 . "AcDbLayerTableRecord")
                   (cons 2 nm)
                   '(70 . 0)
                   (cons 62 cl)
                   '(6 . "Continuous"))))
  nm
)

;; 圆角矩形 (中心点 cx cy, 宽 w, 高 h, 圆角半径 r, 图层 lay)
(defun sp:rrect (cx cy w h r lay / x0 x1 y0 y1 b pts)
  (if (< r 0.3) (setq r 0.3))
  (if (> r (* 0.5 (min w h))) (setq r (* 0.5 (min w h))))
  (setq x0 (- cx (* 0.5 w))
        x1 (+ cx (* 0.5 w))
        y0 (- cy (* 0.5 h))
        y1 (+ cy (* 0.5 h))
        b  (/ (sin (/ pi 8.0)) (cos (/ pi 8.0))))   ; tan22.5 = 0.414214 (90度圆弧凸度)
  (setq pts (list (list (+ x0 r) y0 0.0)
                  (list (- x1 r) y0 b)
                  (list x1 (+ y0 r) 0.0)
                  (list x1 (- y1 r) b)
                  (list (- x1 r) y1 0.0)
                  (list (+ x0 r) y1 b)
                  (list x0 (- y1 r) 0.0)
                  (list x0 (+ y0 r) b)))
  (entmake
    (append
      (list '(0 . "LWPOLYLINE")
            '(100 . "AcDbEntity")
            (cons 8 lay)
            '(100 . "AcDbPolyline")
            '(90 . 8)
            '(70 . 1))
      (apply 'append
             (mapcar '(lambda (v)
                        (list (cons 10 (list (car v) (cadr v)))
                              (cons 42 (caddr v))))
                     pts))))
  (princ)
)

;; 居中文字
(defun sp:text (s x y hgt lay)
  (entmake (list '(0 . "TEXT")
                 '(100 . "AcDbEntity")
                 (cons 8 lay)
                 '(100 . "AcDbText")
                 (cons 10 (list x y 0.0))
                 (cons 40 hgt)
                 (cons 1 s)
                 '(50 . 0.0)
                 '(72 . 1)
                 (cons 11 (list x y 0.0))
                 '(73 . 2)))
  (princ)
)

;; 生成图块定义 (原点为面板中心, 固定 86x86 / 3mm 倒角 / 无装饰框)
(defun sp:build (nm n lay / m gap kw kh sx i)
  (entmake (list '(0 . "BLOCK")
                 (cons 2 nm)
                 '(70 . 0)
                 '(10 0.0 0.0 0.0)
                 (cons 8 lay)))
  (sp:rrect 0.0 0.0 *SP:W* *SP:H* *SP:R* lay)      ; 面板外框(3mm 倒角)
  (setq m   (* 0.10 (min *SP:W* *SP:H*))           ; 按键区边距
        gap (* 0.035 (min *SP:W* *SP:H*))          ; 按键间隙
        kh  (- *SP:H* (* 2.0 m))
        kw  (/ (- (- *SP:W* (* 2.0 m)) (* (1- n) gap)) n)
        sx  (+ (* -0.5 *SP:W*) m)
        i   0)
  (while (< i n)
    (sp:rrect (+ sx (* i (+ kw gap)) (* 0.5 kw)) 0.0 kw kh (* 0.02 (min *SP:W* *SP:H*)) lay)
    (setq i (1+ i)))
  (if *SP:TXT*
    (sp:text (nth n '("" "单联开关" "双联开关" "三联开关" "四联开关"))
             0.0
             (- (* -0.5 *SP:H*) (* 0.14 *SP:H*))
             (* 0.05 *SP:H*)
             lay))
  (entmake '((0 . "ENDBLK")))
  nm
)

;; 按当前设置取图块名(不存在则创建)
(defun sp:block ( / nm)
  (setq nm (strcat "SP-SW-"
                   (itoa *SP:N*) "-86x86"
                   (if *SP:TXT* "-T" "")))
  (if (not (tblsearch "BLOCK" nm))
    (sp:build nm *SP:N* *SP:LAYER*))
  nm
)

;; 命令行状态平铺文本
(defun sp:status ( / )
  (strcat "当前: 开关联数=" (itoa *SP:N*)
          "  文字标注=" (if *SP:TXT* "是" "否")
          "  旋转角度=" (rtos *SP:ANG* 2 1) "度")
)

;; 插入一个面板, 返回 T
(defun sp:place (nm pt / )
  (entmake (list '(0 . "INSERT")
                 (cons 8 *SP:LAYER*)
                 (cons 2 nm)
                 (cons 10 (list (car pt) (cadr pt) 0.0))
                 '(41 . 1.0)
                 '(42 . 1.0)
                 '(43 . 1.0)
                 (cons 50 (* pi (/ *SP:ANG* 180.0)))))
  T
)

;; ------------------- 主命令 -------------------
(defun c:CCC ( / k tmp pt nm cnt done)
  (setq *SP:OLDERR*  *error*
        *SP:OLDECHO* (getvar "cmdecho"))
  (setq *error* sp:err)
  (setvar "cmdecho" 0)
  (sp:mklayer *SP:LAYER* *SP:COLOR*)

  (princ "\n===========  开关面板  CCC  (86x86)  ===========")
  (princ "\n直接点取插入点开始放置; N=改联数, T=改文字标注, R=改角度; 回车或 ESC 退出。")

  (setq cnt  0
        done nil)
  (while (not done)
    (initget "N T R")
    (setq pt (getpoint (strcat "\n" (sp:status)
                               "  指定插入点 [联数(N)/标注(T)/角度(R)] <退出>: ")))
    (cond
      ;; N: 改联数
      ((eq pt "N")
       (initget 6)
       (setq tmp (getint (strcat "\n开关联数 [1~4] <" (itoa *SP:N*) ">: ")))
       (if (and tmp (<= 1 tmp 4))
         (setq *SP:N* tmp)
         (if tmp (princ "\n联数只能 1~4, 保持原值。"))))
      ;; T: 改是否标注联数文字
      ((eq pt "T")
       (initget "Y N")
       (setq k (getkword (strcat "\n是否标注联数文字? [是(Y)/否(N)] <"
                                 (if *SP:TXT* "是" "否") ">: ")))
       (cond ((= k "Y") (setq *SP:TXT* T))
             ((= k "N") (setq *SP:TXT* nil))))
      ;; R: 改旋转角度
      ((eq pt "R")
       (setq tmp (getreal (strcat "\n旋转角度(度) <" (rtos *SP:ANG* 2 1) ">: ")))
       (if tmp (setq *SP:ANG* tmp)))
      ;; 普通点: 插入面板
      (pt
       (setq nm (sp:block))
       (sp:place nm pt)
       (setq cnt (1+ cnt)))
      ;; 回车: 结束
      (T (setq done T))))

  (princ (strcat "\n已插入 " (itoa cnt) " 个 "
                 (itoa *SP:N*) " 联开关面板 (86x86, "
                 (if *SP:TXT* "有" "无") "文字标注, 旋转 "
                 (rtos *SP:ANG* 2 1) "度)。"))
  (setvar "cmdecho" *SP:OLDECHO*)
  (setq *error* *SP:OLDERR*)
  (princ)
)

;; ------------------- 加载提示 -------------------
(princ "\n[CCC] 开关面板程序已加载(86x86), 命令行输入 CCC 运行。")
(princ)