;;; ============================================================
;;; C1.lsp  -  插座面板 (86x86, 四角 R10)
;;;            三插在左上 (下排两孔斜 45 度, 已下移 10) / 两插在右下
;;; 命令: C1   点击位置即放置, 可连续放置, 回车/ESC 结束
;;; 图块: C1_S32V5_86 (自动创建, 原点在面板中心)
;;; 无旋转 / 无比例 / 无文字标注
;;; ============================================================

;; 90 度圆弧 bulge = tan(45/2); 本图形顶点顺时针排列, 凸圆角取负值
(setq *c1-bulge* -0.4142135624)
(setq *c1-blk* "C1_S32V5_86")
(setq *c1-45* 0.7853981634)     ; 45 度

;; 绕 (cx cy) 旋转的坐标变换
(defun c1-rp (x y ca sa cx cy)
  (list (+ cx (- (* x ca) (* y sa)))
        (+ cy (+ (* x sa) (* y ca))))
)

;; 圆头插槽: 中心 (cx cy), len=总长, wid=槽宽, ang=旋转角(弧度, 逆时针)
;; 两端为半圆(180 度, bulge 绝对值 1.0); 旋转不改变绕向, 仍取 -1.0
(defun c1-slot (cx cy len wid ang / hx hy ca sa p1 p2 p3 p4)
  (setq hx (/ wid 2.0)
        hy (- (/ len 2.0) (/ wid 2.0)))
  (if (null ang) (setq ang 0.0))
  (setq ca (cos ang) sa (sin ang))
  (setq p1 (c1-rp (- hx) (- hy) ca sa cx cy)
        p2 (c1-rp (- hx) hy ca sa cx cy)
        p3 (c1-rp hx hy ca sa cx cy)
        p4 (c1-rp hx (- hy) ca sa cx cy))
  (entmake (list '(0 . "LWPOLYLINE")
                 '(100 . "AcDbEntity")
                 '(100 . "AcDbPolyline")
                 '(90 . 4)
                 '(70 . 1)
                 (cons 10 p1)
                 (cons 10 p2) (cons 42 -1.0)
                 (cons 10 p3)
                 (cons 10 p4) (cons 42 -1.0)))
)

;; 创建图块: R10 圆角面板 + 左上三插 + 右下两插
(defun c1-mkblock (blk / b lp)
  (setq b *c1-bulge*)
  (entmake (list '(0 . "BLOCK")
                 '(100 . "AcDbEntity")
                 '(100 . "AcDbBlockBegin")
                 (cons 2 blk)
                 '(70 . 2)
                 '(10 0.0 0.0 0.0)))
  ;; 面板外框 86x86, 四角 R10
  (setq lp (list '(0 . "LWPOLYLINE")
                 '(100 . "AcDbEntity")
                 '(100 . "AcDbPolyline")
                 '(90 . 8)
                 '(70 . 1)))
  (setq lp (append lp
                   (list (cons 10 (list -33.0 -43.0)) (cons 42 b)
                         (cons 10 (list -43.0 -33.0))
                         (cons 10 (list -43.0 33.0))  (cons 42 b)
                         (cons 10 (list -33.0 43.0))
                         (cons 10 (list 33.0 43.0))   (cons 42 b)
                         (cons 10 (list 43.0 33.0))
                         (cons 10 (list 43.0 -33.0))  (cons 42 b)
                         (cons 10 (list 33.0 -43.0)))))
  (entmake lp)
  ;; ---- 左上角: 三插, 组中心 (-18,18) ----
  (c1-slot -18.0 24.5 13.0 4.4 0.0)          ; 地线孔, 竖放
  (c1-slot -27.5 2.5 15.0 4.4 *c1-45*)       ; 左下孔: 上端向左斜 45 度 (较原位置下移 10)
  (c1-slot -8.5 2.5 15.0 4.4 (- *c1-45*))    ; 右下孔: 上端向右斜 45 度 (较原位置下移 10)
  ;; ---- 右下角: 两插, 组中心 (18,-18) ----
  (c1-slot 10.0 -18.0 15.0 4.4 0.0)          ; 左孔
  (c1-slot 26.0 -18.0 15.0 4.4 0.0)          ; 右孔
  (entmake '((0 . "ENDBLK") (100 . "AcDbEntity") (100 . "AcDbBlockEnd")))
  (tblsearch "BLOCK" blk)
)

;; 在点位插入图块 (entmake 直接生成, 不走命令行)
(defun c1-place (blk pt)
  (entmake (list '(0 . "INSERT")
                 '(100 . "AcDbEntity")
                 '(100 . "AcDbBlockReference")
                 (cons 2 blk)
                 (cons 10 pt)
                 '(41 . 1.0) '(42 . 1.0) '(43 . 1.0)
                 '(50 . 0.0)))
)

(defun c:C1 (/ blk pt n)
  (setq blk *c1-blk*)
  (if (not (tblsearch "BLOCK" blk))
    (c1-mkblock blk)
  )
  (setq n 0)
  (princ "\n[C1] 点击位置放置插座面板(左上三插 + 右下两插), 可连续点击, 回车结束。")
  (while (setq pt (getpoint "\n指定插座面板位置: "))
    (c1-place blk pt)
    (setq n (1+ n))
  )
  (princ (strcat "\n[C1] 完成, 共放置 " (itoa n) " 个插座面板。"))
  (princ)
)

(princ "\n[C1] 已加载。输入 C1 放置 86x86 插座面板(左上三插 + 右下两插)。")
(princ)