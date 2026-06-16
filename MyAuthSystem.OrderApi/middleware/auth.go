package middleware

import (
	"context"
	"fmt"
	"net/http"
	"os"
	"strings"

	"github.com/golang-jwt/jwt/v5"
)

// สร้างประเภทข้อมูลพิเศษสำหรับเก็บไว้ใน Context ของ Go
type contextKey string

const UserIDKey contextKey = "userId"

func JwtAuthMiddleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		// 1. ดึงข้อมูลจาก HTTP Authorization Header
		authHeader := r.Header.Get("Authorization")
		if authHeader == "" {
			http.Error(w, `{"message": "จำเป็นต้องใส่ Authorization Header"}`, http.StatusUnauthorized)
			return
		}

		// 2. ตรวจสอบรูปแบบ "Bearer <token>"
		parts := strings.Split(authHeader, " ")
		if len(parts) != 2 || parts[0] != "Bearer" {
			http.Error(w, `{"message": "รูปแบบโทเค็นไม่ถูกต้อง (Bearer <token>)"}`, http.StatusUnauthorized)
			return
		}

		tokenString := parts[1]

		// ทำการ Parse และตรวจสอบความถูกต้องของ JWT (ทั้งลายเซ็นดิจิทัลและเวลาหมดอายุ)
		jwtSecret := os.Getenv("JWT_SECRET")
		if jwtSecret == "" {
			http.Error(w, `{"message": "ระบบภายในขัดข้อง (Missing JWT Secret)"}`, http.StatusInternalServerError)
			return
		}
		jwtKey := []byte(jwtSecret)

		token, err := jwt.Parse(tokenString, func(token *jwt.Token) (interface{}, error) {
			// ตรวจสอบว่าใช้อัลกอร์ิทึมแบบ HMAC SHA256 ตามที่เราตั้งค่าไว้ใน .NET
			if _, ok := token.Method.(*jwt.SigningMethodHMAC); !ok {
				return nil, fmt.Errorf("unexpected signing method: %v", token.Header["alg"])
			}
			return jwtKey, nil
		})

		// ถ้าตั๋วปลอม หมดอายุ หรือถูกแก้ไขระหว่างทาง err จะไม่เป็น nil
		if err != nil || !token.Valid {
			http.Error(w, `{"message": "ตั๋วหมดอายุ หรือ ลายเซ็นไม่ถูกต้อง"}`, http.StatusUnauthorized)
			return
		}

		// 4. ดึงข้อมูลจากพาร์ท Payload (Claims) ออกมาใช้งาน
		if claims, ok := token.Claims.(jwt.MapClaims); ok && token.Valid {
			// ดึงค่า sub (User ID) ที่ฝั่ง .NET
			userID, _ := claims["sub"].(string)

			// ส่งข้อมูล User ID ฝังเข้าไปใน Request Context เพื่อให้ Endpoint หลังบ้านเอาไปใช้ต่อ
			ctx := context.WithValue(r.Context(), UserIDKey, userID)
			next.ServeHTTP(w, r.WithContext(ctx))
			return
		}

		http.Error(w, `{"message": "ไม่สามารถอ่านข้อมูลในตั๋วได้"}`, http.StatusUnauthorized)
	})
}
