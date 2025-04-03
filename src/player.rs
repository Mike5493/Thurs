use raylib::math::Vector2;

pub struct Player {
    pub pos: Vector2,
    pub dir: Vector2,
    pub plane: Vector2,
    pub move_speed: f32,
    pub rot_speed: f32,
}

impl Player {
    pub fn new() -> Self {
        Self {
            pos: Vector2::new(2.0, 2.0),
            dir: Vector2::new(-1.0, 0.0),
            plane: Vector2::new(0.0, 0.66),
            move_speed: 0.05,
            rot_speed: 0.03,
        }
    }
}