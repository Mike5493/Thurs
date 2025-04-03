use crate::map::{WORLD_MAP, MAP_WIDTH, MAP_HEIGHT};
use raylib::math::{Vector2};

#[derive(Debug, Clone, Copy)]
pub struct IVec2 {
    pub x: i32,
    pub y: i32,
}

impl IVec2 {
    pub fn new(x: i32, y: i32) -> Self {
        IVec2 { x, y }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct RayHit {
    pub distance: f32,
    pub hit_pos: Vector2,
    pub map_pos: IVec2,
    pub side: usize,
    pub step: IVec2,
}

pub fn cast_ray(pos: Vector2, dir: Vector2) -> Option<RayHit> {
    let mut map_pos = IVec2::new(pos.x.floor() as i32, pos.y.floor() as i32);

    let delta_dist = Vector2::new(
      if dir.x != 0.0 { (1.0 / dir.x).abs()} else { f32::INFINITY},
      if dir.y != 0.0 { (1.0 / dir.y).abs()} else { f32::INFINITY},
    );

    let (step_x, mut side_dist_x) = if dir.x < 0.0 {
        (-1, (pos.x - map_pos.x as f32) * delta_dist.x)
    } else {
        (1, (map_pos.x as f32 + 1.0 - pos.x) * delta_dist.x)
    };
    let (step_y, mut side_dist_y) = if dir.y < 0.0 {
        (-1, (pos.y - map_pos.y as f32) * delta_dist.y)
    } else {
        (1, (map_pos.y as f32 + 1.0 - pos.y) * delta_dist.y)
    };

    let step = IVec2::new(step_x, step_y);
    let mut side = 0;

    for _ in 0..64 {
        if side_dist_x < side_dist_y {
            side_dist_x += delta_dist.x;
            map_pos.x += step_x;
            side = 0;
        } else {
            side_dist_y += delta_dist.y;
            map_pos.y += step_y;
            side = 1;
        }

        if map_pos.x < 0 || map_pos.y < 0 || map_pos.x as usize >= MAP_WIDTH || map_pos.y as usize >= MAP_HEIGHT {
            return None;
        }

        if WORLD_MAP[map_pos.y as usize][map_pos.x as usize] > 0 {
            let distance = if side == 0 {
                (map_pos.x as f32 - pos.x + (1 - step_x) as f32 * 0.5) / dir.x
            } else {
                (map_pos.y as f32 - pos.y + (1 - step_y) as f32 * 0.5) / dir.y
            };

            let hit_pos = pos + dir * distance;

            return Some(RayHit {
                distance,
                hit_pos,
                map_pos,
                side,
                step,
            });
        }
    }
    None
}