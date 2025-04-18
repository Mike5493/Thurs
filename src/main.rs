mod engine;
mod map;
mod player;
mod raycaster;

use engine::run_game;
use raylib::prelude::*;

fn main() {
    let (mut rl, thread) = raylib::init()
        .size(1080, 720)
        .title("THURS")
        .vsync()
        .build();
    rl.set_target_fps(60);
    run_game(&mut rl, &thread);
}
