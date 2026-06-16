package middleware

import (
	"net/http"
	"net/http/httptest"
	"os"
	"testing"
	"time"

	"github.com/golang-jwt/jwt/v5"
)

// ฟังก์ชันจำลองการสร้างตั๋วสำหรับใช้ในตรรกะการเทส
func createMockToken(userID string, expired bool) string {
	secret := "TestSecretKeyThatIsLongEnoughForHmac256_2026!"
	os.Setenv("JWT_SECRET", secret) // ตั้งค่า Env ชั่วคราวสำหรับการเทส

	var exp time.Time
	if expired {
		exp = time.Now().Add(-time.Hour) // ตั๋วหมดอายุไปแล้ว 1 ชั่วโมง
	} else {
		exp = time.Now().Add(time.Hour) // ตั๋วใช้งานได้อีก 1 ชั่วโมง
	}

	claims := jwt.MapClaims{
		"sub": userID,
		"exp": exp.Unix(),
	}

	token := jwt.NewWithClaims(jwt.SigningMethodHS256, claims)
	tokenString, _ := token.SignedString([]byte(secret))
	return tokenString
}

func TestJwtAuthMiddleware(t *testing.T) {
	// สร้าง Handler ปลายทางจำลอง เพื่อเช็กว่าถ้าตั๋วผ่าน จะวิ่งมาถึงตรงนี้ไหม
	nextHandler := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		userID := r.Context().Value(UserIDKey).(string)
		w.WriteHeader(http.StatusOK)
		w.Write([]byte(userID))
	})

	middlewareToTest := JwtAuthMiddleware(nextHandler)

	// เคสที่ 1: ไม่ส่ง Authorization Header มาเลย -> ต้องโดน 401
	t.Run("Missing_Authorization_Header", func(t *testing.T) {
		req := httptest.NewRequest("GET", "/api/orders/my", nil)
		rr := httptest.NewRecorder()

		middlewareToTest.ServeHTTP(rr, req)

		if rr.Code != http.StatusUnauthorized {
			t.Errorf("คาดหวัง 401 แต่ได้ %d", rr.Code)
		}
	})

	// เคสที่ 2: ส่งตั๋วหมดอายุมา -> ต้องโดน 401
	t.Run("Expired_Token", func(t *testing.T) {
		expiredToken := createMockToken("user_test_123", true)
		req := httptest.NewRequest("GET", "/api/orders/my", nil)
		req.Header.Set("Authorization", "Bearer "+expiredToken)
		rr := httptest.NewRecorder()

		middlewareToTest.ServeHTTP(rr, req)

		if rr.Code != http.StatusUnauthorized {
			t.Errorf("ตั๋วหมดอายุควรได้ 401 แต่ได้ %d", rr.Code)
		}
	})

	// เคสที่ 3: ตั๋วถูกต้องสมบูรณ์ -> ต้องได้ 200 OK และส่ง User ID ผ่านเข้าไปได้
	t.Run("Valid_Token_Success", func(t *testing.T) {
		validToken := createMockToken("user_test_123", false)
		req := httptest.NewRequest("GET", "/api/orders/my", nil)
		req.Header.Set("Authorization", "Bearer "+validToken)
		rr := httptest.NewRecorder()

		middlewareToTest.ServeHTTP(rr, req)

		if rr.Code != http.StatusOK {
			t.Errorf("ตั๋วปกติควรได้ 200 แต่ได้ %d", rr.Code)
		}

		if rr.Body.String() != "user_test_123" {
			t.Errorf("UserID ใน Context ผิดพลาด: ได้ %s", rr.Body.String())
		}
	})
}
