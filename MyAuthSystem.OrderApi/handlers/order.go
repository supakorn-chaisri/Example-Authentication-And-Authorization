package handlers

import (
	"context"
	"encoding/json"
	"log"
	"net/http"

	"MyAuthSystem.OrderApi/middleware"
	"github.com/jackc/pgx/v5/pgxpool"
)

type OrderResponse struct {
	OrderID     string  `json:"orderId"`
	ProductName string  `json:"productName"`
	Price       float64 `json:"price"`
}

func GetMyOrdersHandler(w http.ResponseWriter, r *http.Request, dbpool *pgxpool.Pool) {
	w.Header().Set("Content-Type", "application/json")

	// ดึง User ID มาจากตั๋ว JWT ของฝั่ง .NET Identity Server
	userID, ok := r.Context().Value(middleware.UserIDKey).(string)
	if !ok || userID == "" {
		log.Println("❌ Error: ไม่สามารถดึง UserID ออกมาจาก Request Context ได้")
		w.WriteHeader(http.StatusUnauthorized)
		json.NewEncoder(w).Encode(map[string]string{"message": "ไม่พบข้อมูลผู้ใช้ในระบบ"})
		return
	}

	// ดึงข้อมูลเฉพาะของตนเอง
	query := `SELECT order_id, product_name, price FROM orders WHERE user_id = $1`

	rows, err := dbpool.Query(context.Background(), query, userID)
	if err != nil {
		log.Printf("❌ Database Query Error (User: %s): %v\n", userID, err)
		w.WriteHeader(http.StatusInternalServerError)
		json.NewEncoder(w).Encode(map[string]string{"message": "เกิดข้อผิดพลาดภายในระบบฐานข้อมูล"})
		return
	}
	defer rows.Close()

	// เพื่อป้องกันไม่ให้ Go พ่นคำว่า null ออกไปใน JSON ในกรณีที่ผู้ใช้คนนี้ยังไม่มีประวัติซื้อของ
	orders := make([]OrderResponse, 0)

	// วนอ่านข้อมูล
	for rows.Next() {
		var order OrderResponse
		err := rows.Scan(&order.OrderID, &order.ProductName, &order.Price)
		if err != nil {
			log.Printf("❌ Database Scan Error: %v\n", err)
			w.WriteHeader(http.StatusInternalServerError)
			json.NewEncoder(w).Encode(map[string]string{"message": "ไม่สามารถแปลงข้อมูลจากฐานข้อมูลได้"})
			return
		}
		orders = append(orders, order)
	}
	if err = rows.Err(); err != nil {
		log.Printf("❌ Rows iteration error: %v\n", err)
	}

	responseData := map[string]interface{}{
		"status":  "success",
		"message": "ดึงข้อมูลจาก PostgreSQL สำเร็จ",
		"userId":  userID,
		"orders":  orders,
	}

	w.WriteHeader(http.StatusOK)
	json.NewEncoder(w).Encode(responseData)
}
