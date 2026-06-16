package main

import (
	"context"
	"log"
	"net/http"
	"os"

	"MyAuthSystem.OrderApi/handlers"
	"MyAuthSystem.OrderApi/middleware"

	"github.com/gorilla/mux"
	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/joho/godotenv"
)

func main() {
	// 1. 🚀 สั่งโหลดไฟล์ .env เข้าสู่ระบบความปลอดภัยของเครื่อง
	err := godotenv.Load()
	if err != nil {
		log.Println("⚠️ ไม่พบไฟล์ .env ระบบจะพยายามดึงค่าจาก Environment Variables ของ OS แทน")
	}

	// 2. ดึงค่า Connection String ออกมาจาก Env
	connStr := os.Getenv("DB_CONNECTION_STRING")
	if connStr == "" {
		log.Fatal("❌ ไม่พบค่า DB_CONNECTION_STRING ใน Environment Variables!")
	}

	// 3. เริ่มต้นเชื่อมต่อฐานข้อมูลด้วยค่าที่ดึงมา
	dbpool, err := pgxpool.New(context.Background(), connStr)
	if err != nil {
		log.Fatalf("ไม่สามารถเชื่อมต่อ PostgreSQL ได้: %v\n", err)
	}
	defer dbpool.Close()

	err = dbpool.Ping(context.Background())
	if err != nil {
		log.Fatalf("Database ping ล้มเหลว: %v\n", err)
	}
	log.Println("🐘 เชื่อมต่อ PostgreSQL ผ่านค่าความลับใน .env สำเร็จ!")

	r := mux.NewRouter()

	// 1. สร้างกลุ่มของ Route สำหรับ API
	apiRoute := r.PathPrefix("/api").Subrouter()

	// 2. กำหนด Endpoint และสั่งให้มันวิ่งผ่าน "JwtAuthMiddleware" เพื่อคัดกรองตั๋วก่อนเสมอ
	apiRoute.Handle("/orders/my", middleware.JwtAuthMiddleware(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		handlers.GetMyOrdersHandler(w, r, dbpool)
	}))).Methods("GET")

	log.Println("🚀 Go Order API กำลังรันที่พอร์ต :5001...")
	log.Fatal(http.ListenAndServe(":5001", r))
}
