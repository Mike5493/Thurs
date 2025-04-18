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
        let dir = Vector2::new(-1.0, 0.0);
        let fov_scale = 0.5;
        let plane = Vector2::new(0.0, fov_scale);

        Self {
            pos: Vector2::new(3.5, 3.5),
            dir,
            plane,
            move_speed: 0.05,
            rot_speed: 0.03,
        }
    }
    pub fn rotate(&mut self, angle: f32) {
        let cos = angle.cos();
        let sin = angle.sin();

        let old_dir = self.dir;
        self.dir.x = old_dir.x * cos - old_dir.y * sin;
        self.dir.y = old_dir.x * sin + old_dir.y * cos;

        let old_plane = self.plane;
        self.plane.x = old_plane.x * cos - old_plane.y * sin;
        self.plane.y = old_plane.x * sin + old_plane.y * cos;

        self.dir = self.dir.normalized();
        self.plane = self.plane.normalized();
    }
}
