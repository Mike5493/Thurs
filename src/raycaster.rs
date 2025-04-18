use crate::map::{MAP_HEIGHT, MAP_WIDTH, WORLD_MAP};
use raylib::math::Vector2;

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

pub fn cast_ray(origin: Vector2, dir: Vector2) -> Option<RayHit> {
    let mut map_pos = IVec2::new(origin.x as i32, origin.y as i32);
    let delta_dist = Vector2::new(
        if dir.x != 0.0 {
            (1.0 / dir.x).abs()
        } else {
            f32::INFINITY
        },
        if dir.y != 0.0 {
            (1.0 / dir.y).abs()
        } else {
            f32::INFINITY
        },
    );

    let (step_x, mut side_dist_x) = if dir.x < 0.0 {
        (-1, (origin.x - map_pos.x as f32) * delta_dist.x)
    } else {
        (1, (map_pos.x as f32 + 1.0 - origin.x) * delta_dist.x)
    };

    let (step_y, mut side_dist_y) = if dir.y < 0.0 {
        (-1, (origin.y - map_pos.y as f32) * delta_dist.y)
    } else {
        (1, (map_pos.y as f32 + 1.0 - origin.y) * delta_dist.y)
    };

    let step = IVec2::new(step_x, step_y);
    let mut side = 0;

    for _ in 0..256 {
        const EPSILON: f32 = 0.0001;
        if (side_dist_x - side_dist_y).abs() < EPSILON {
            // Check for BOTH directions
            let next_x = map_pos.x + step_x;
            let next_y = map_pos.y + step_y;
            let hit_x = next_x >= 0
                && next_x < MAP_WIDTH as i32
                && WORLD_MAP[map_pos.y as usize][next_x as usize] > 0;
            let hit_y = next_y >= 0
                && next_y < MAP_HEIGHT as i32
                && WORLD_MAP[next_y as usize][map_pos.x as usize] > 0;

            if hit_x || hit_y {
                if hit_x && hit_y {
                    side = if dir.x.abs() > dir.y.abs() { 0 } else { 1 };
                } else {
                    side = if hit_x { 0 } else { 1 };
                }
                if side == 0 {
                    map_pos.x = next_x;
                } else {
                    map_pos.y = next_y;
                }
            }
        } else if side_dist_x < side_dist_y {
            side_dist_x += delta_dist.x;
            map_pos.x += step_x;
            side = 0;
        } else {
            side_dist_y += delta_dist.y;
            map_pos.y += step_y;
            side = 1;
        }

        if map_pos.x < 0
            || map_pos.y < 0
            || map_pos.x as usize >= MAP_WIDTH
            || map_pos.y as usize >= MAP_HEIGHT
        {
            return None;
        }

        if WORLD_MAP[map_pos.y as usize][map_pos.x as usize] > 0 {
            let distance = if side == 0 {
                side_dist_x - delta_dist.x
            } else {
                side_dist_y - delta_dist.y
            };
            let hit_pos = origin + dir * distance;

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
