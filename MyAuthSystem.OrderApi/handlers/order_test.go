package handlers

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"

	"MyAuthSystem.OrderApi/middleware"

	"github.com/pashagolub/pgxmock/v5"
)

func TestGetMyOrdersHandler_Success(t *testing.T) {
	// 1. สร้างฐานข้อมูลจำลอง (Mock DB)
	mock, err := pgxmock.NewPool()
	if err != nil {
		t.Fatalf("ไม่สามารถสร้าง mock pool ได้: %v", err)
	}
	defer mock.Close()

	// 2. กำหนดล่วงหน้าว่า ถ้ามี SQL Query นี้วิ่งเข้ามา พร้อมกับแถม UserId = "user_123"
	// ให้ฐานข้อมูลจำลองนี้ คืนค่าผลลัพธ์กลับไป 1 แถว (Row) เสมือนคิวรี่จาก Postgres จริง
	mock.ExpectQuery(`SELECT order_id, product_name, price FROM orders WHERE user_id = \$1`).
		WithArgs("user_123").
		WillReturnRows(pgxmock.NewRows([]string{"order_id", "product_name", "price"}).
			AddRow("ORD-TEST-01", "Enterprise Smart Plug", 750.00))

	// 3. จำลอง Request และยัดค่า UserID ใส่ Context เหมือนที่ Middleware ทำ
	req := httptest.NewRequest("GET", "/api/orders/my", nil)
	ctx := context.WithValue(req.Context(), middleware.UserIDKey, "user_123")
	req = req.WithContext(ctx)

	rr := httptest.NewRecorder()

	// 4. เรียกใช้ Handler ตัวจริงโดยส่ง mock pool เข้าไปแทนของจริง!
	GetMyOrdersHandler(rr, req, mock)

	// 5. ตรวจสอบผลลัพธ์ (Assertions)
	if rr.Code != http.StatusOK {
		t.Errorf("ควรได้รหัส 200 แต่ระบบส่งกลับมาเป็น: %d", rr.Code)
	}

	// แกะ JSON ออกมาเช็กว่าโครงสร้างถูกต้องไหม
	var response map[string]interface{}
	if err := json.Unmarshal(rr.Body.Bytes(), &response); err != nil {
		t.Fatalf("ไม่สามารถ Parse JSON Response ได้: %v", err)
	}

	if response["status"] != "success" {
		t.Errorf("Status ใน JSON คาดหวัง success แต่ได้รับ %v", response["status"])
	}

	// ตรวจสอบว่า mock ทำงานครบถ้วนตามที่เราตั้งเงื่อนไขไว้ไหม
	if err := mock.ExpectationsWereMet(); err != nil {
		t.Errorf("ฐานข้อมูลจำลองทำงานไม่ตรงตามแผน: %v", err)
	}
}
