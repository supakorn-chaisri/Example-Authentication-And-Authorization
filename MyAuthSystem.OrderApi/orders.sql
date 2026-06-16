-- สร้างตารางเก็บข้อมูลออเดอร์
CREATE TABLE orders (
    id SERIAL PRIMARY KEY,
    order_id VARCHAR(50) NOT NULL UNIQUE,
    user_id VARCHAR(255) NOT NULL,
    product_name VARCHAR(100) NOT NULL,
    price NUMERIC(10, 2) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- เพิ่มข้อมูลทดลอง (กรุณาเปลี่ยนค่า user_id ให้ตรงกับไอดีผู้ใช้ที่คุณสมัครสมาชิกใน .NET)
INSERT INTO orders (order_id, user_id, product_name, price) VALUES
('ORD-2026-0001', 'ใส่_USER_ID_จาก_DOTNET_ตรงนี้', 'Smart Plug Pro', 590.00),
('ORD-2026-0002', 'ใส่_USER_ID_จาก_DOTNET_ตรงนี้', 'Wireless Earbuds', 1290.00),
('ORD-2026-0003', 'user_id_คนอื่น', 'Mechanical Keyboard', 2500.00);