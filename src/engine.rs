//==========================================================================|
//                 <|> ThursEngine ported to Rust <|>                       |
//--------------------------------------------------------------------------|
//  Author: Mikey                                                           |
//  Date: 04/17/2025                                                        |
//                                                                          |
//==========================================================================|

use crate::map::{MAP_HEIGHT, MAP_WIDTH, WORLD_MAP};
use crate::player::Player;
use crate::raycaster::cast_ray;
use raylib::prelude::*;

fn collision_check(circle_pos: Vector2, radius: f32, rect: Rectangle) -> bool {
    let closest_x = circle_pos.x.clamp(rect.x, rect.x + rect.width);
    let closest_y = circle_pos.y.clamp(rect.y, rect.y + rect.height);

    let dx = circle_pos.x - closest_x;
    let dy = circle_pos.y - closest_y;

    dx * dx + dy * dy < radius * radius
}

fn is_colliding(pos: Vector2, radius: f32) -> bool {
    (0..MAP_HEIGHT).any(|y| {
        (0..MAP_WIDTH).any(|x| {
            WORLD_MAP[y][x] != 0
                && collision_check(pos, radius, Rectangle::new(x as f32, y as f32, 1.0, 1.0))
        })
    })
}

pub fn run_game(rl: &mut RaylibHandle, thread: &RaylibThread) {
    let mut player = Player::new();
    let wall_texture = rl
        .load_texture(thread, "assets/wall.png")
        .expect("Missing Texture");
    wall_texture.set_texture_filter(thread, TextureFilter::TEXTURE_FILTER_POINT);

    let ceiling_color = Color::new(20, 20, 30, 255);
    let floor_color = Color::new(40, 30, 20, 255);

    rl.set_target_fps(60);
    rl.disable_cursor();

    let (screen_w, screen_h) = (rl.get_screen_width(), rl.get_screen_height());
    let projected_plane = screen_w as f32 / 2.0;
    const MOUSE_SENSITIVITY: f32 = 0.005;
    const COLLISION_RADIUS: f32 = 0.1;

    while !rl.window_should_close() {
        let forward = player.dir * player.move_speed;
        let strafe = Vector2::new(-player.dir.y, player.dir.x) * player.move_speed;

        // Movement
        for key in [
            (KeyboardKey::KEY_W, forward),
            (KeyboardKey::KEY_S, -forward),
            (KeyboardKey::KEY_A, strafe),
            (KeyboardKey::KEY_D, -strafe),
        ] {
            if rl.is_key_down(key.0) {
                let move_dir = key.1;
                let try_pos = player.pos + move_dir;

                // Wall sliding
                if !is_colliding(try_pos, COLLISION_RADIUS) {
                    player.pos = try_pos;
                } else {
                    let x_only = Vector2::new(try_pos.x, player.pos.y);
                    let y_only = Vector2::new(player.pos.x, try_pos.y);

                    if !is_colliding(try_pos, COLLISION_RADIUS) {
                        player.pos.x = x_only.x;
                    }
                    if !is_colliding(y_only, COLLISION_RADIUS) {
                        player.pos.y = y_only.y;
                    }
                }
            }
        }

        // Rotation
        let delta_x = -rl.get_mouse_delta().x;
        player.rotate(delta_x * MOUSE_SENSITIVITY);

        let mut d = rl.begin_drawing(thread);

        d.clear_background(ceiling_color);
        d.draw_rectangle(0, screen_h / 2, screen_w, screen_h / 2, floor_color);

        for x in 0..screen_w {
            let camera_x = 2.0 * x as f32 / screen_w as f32 - 1.0;
            let ray_dir = player.dir + player.plane * camera_x;

            if let Some(hit) = cast_ray(player.pos, ray_dir) {
                let _ray_dir_norm = ray_dir.normalized();
                let cos_angle = _ray_dir_norm.dot(player.dir);
                let corrected_dist = (hit.distance / cos_angle).max(0.2);
                let wall_height = projected_plane / corrected_dist;

                let draw_start = (screen_h as f32 / 2.0 - wall_height / 2.0).max(0.0).floor();
                let draw_end = (screen_h as f32 / 2.0 + wall_height / 2.0)
                    .min(screen_h as f32)
                    .ceil();
                let height = draw_end - draw_start;

                let mut wall_x = if hit.side == 0 {
                    hit.hit_pos.y
                } else {
                    hit.hit_pos.x
                };
                wall_x -= wall_x.floor();

                let mut tex_x = (wall_x * wall_texture.width() as f32).round() as i32;
                if (hit.side == 0 && ray_dir.x > 0.0) || (hit.side == 1 && ray_dir.y < 0.0) {
                    tex_x = wall_texture.width() - tex_x - 1;
                }
                tex_x = tex_x.clamp(0, wall_texture.width() - 1);

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
                        y: draw_start,
                        width: 1.0,
                        height,
                    },
                    Vector2::zero(),
                    0.0,
                    Color::WHITE,
                );
            }
        }
    }
}
