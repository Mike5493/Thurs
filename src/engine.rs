use raylib::prelude::*;
use crate::player::Player;
use crate::map::{WORLD_MAP, MAP_WIDTH, MAP_HEIGHT};
use crate::raycaster::cast_ray;

fn can_move(pos: Vector2) -> bool {
    let x = pos.x as usize;
    let y = pos.y as usize;
    if x >= MAP_WIDTH || y >= MAP_HEIGHT {
        return false;
    }
    WORLD_MAP[y][x] == 0
}

pub fn run_game(rl: &mut RaylibHandle, thread: &RaylibThread) {
    let mut player = Player::new();

    let wall_texture = rl
        .load_texture(&thread, "assets/wall.png")
        .expect("Failed to load wall texture");

    while !rl.window_should_close() {
        let forward = player.dir * player.move_speed;
        let strafe = Vector2::new(-player.dir.y, player.dir.x) * player.move_speed;
        if rl.is_key_down(KeyboardKey::KEY_W) {
            let next_x = player.pos + Vector2::new(forward.x, 0.0);
            let next_y = player.pos + Vector2::new(0.0, forward.y);

            if can_move(next_x) {
                player.pos.x = next_x.x;
            }
            if can_move(next_y) {
                player.pos.y = next_y.y;
            }
        }

        if rl.is_key_down(KeyboardKey::KEY_S) {
            let back = -forward;
            let next_x = player.pos + Vector2::new(back.x, 0.0);
            let next_y = player.pos + Vector2::new(0.0, back.y);

            if can_move(next_x) {
                player.pos.x = next_x.x;
            }
            if can_move(next_y) {
                player.pos.y = next_y.y;
            }
        }

        if rl.is_key_down(KeyboardKey::KEY_A) {
            let next_x = player.pos + Vector2::new(strafe.x, 0.0);
            let next_y = player.pos + Vector2::new(0.0, strafe.y);

            if can_move(next_x) {
                player.pos.x = next_x.x;
            }
            if can_move(next_y) {
                player.pos.y = next_y.y;
            }
        }

        if rl.is_key_down(KeyboardKey::KEY_D) {
            let right = -strafe;
            let next_x = player.pos + Vector2::new(right.x, 0.0);
            let next_y = player.pos + Vector2::new(0.0, right.y);

            if can_move(next_x) {
                player.pos.x = next_x.x;
            }
            if can_move(next_y) {
                player.pos.y = next_y.y;
            }
        }

        // Mouse turning
        let delta_x = -rl.get_mouse_delta().x;
        let rot = delta_x * 0.005;
        rl.disable_cursor();

        let cos = rot.cos();
        let sin = rot.sin();

        let old_dir_x = player.dir.x;
        player.dir.x = player.dir.x * cos - player.dir.y * sin;
        player.dir.y = old_dir_x * sin + player.dir.y * cos;

        let old_plane_x = player.plane.x;
        player.plane.x = player.plane.x * cos - player.plane.y * sin;
        player.plane.y = old_plane_x * sin + player.plane.y * cos;

        // Draw
        let screen_width = rl.get_screen_width();
        let screen_height = rl.get_screen_height();

        let mut d = rl.begin_drawing(thread);
        d.clear_background(Color::BLACK);

        for x in 0..screen_width {
            let camera_x = 2.0 * x as f32 / screen_width as f32 - 1.0;
            let ray_dir = player.dir + player.plane * camera_x;

            if let Some(hit) = cast_ray(player.pos, ray_dir) {
                let corrected_distance = hit.distance * (ray_dir.dot(player.dir)).abs();
                let line_height = (screen_height as f32 / corrected_distance) as i32;

                let draw_start = (screen_height / 2 - line_height / 2).max(0);
                let draw_end = (screen_height / 2 + line_height / 2).min(screen_height);

                // Calculate x texture coord
                let mut wall_x = if hit.side == 0 {
                    hit.hit_pos.y
                } else {
                    hit.hit_pos.x
                };
                wall_x -= wall_x.floor();

                let mut tex_x = (wall_x * wall_texture.width() as f32) as i32;

                // Draw Vertical Strip
                d.draw_texture_pro(
                    &wall_texture,
                    Rectangle {
                        x: tex_x as f32,
                        y: 0.0,
                        width: 1.0,
                        height: wall_texture.height() as f32,
                    },
                    Rectangle {
                        x: x as f32,
                        y: draw_start as f32,
                        width: 1.0,
                        height: (draw_end - draw_start) as f32,
                    },
                    Vector2::zero(),
                    0.0,
                    Color::WHITE,
                );
            }
        }
    }
}